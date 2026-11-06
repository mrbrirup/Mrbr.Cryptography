# Authenticated AES key recovery

`Mrbr.Cryptography.Vss.KeyManager` is an optional adapter in the integration
solution. It references KeyManager and `Mrbr.Cryptography.Vss.Authentication`.
Its public class is `Mrbr.Cryptography.Vss.KeyManager.AuthenticatedKeySharing`.
EncryptionManager is used by tests, benchmarks and the example below through
the existing `AesGcmKeyDecryptor`; the adapter itself has no encryption dependency.
The original standalone solution remains independent of the manager checkouts.

The four steps are:

1. Replay an existing AES key using `IKeyService.GetKey(handle, Span<byte>)`.
2. Create fresh Pedersen shares and sign their commitments and recovery metadata.
3. Authenticate expectations and recover the original key from a quorum.
4. Import the recovered key into the existing raw-key decryptor and decrypt the
   original EncryptionManager ciphertext.

Only creation requires KeyManager. Recovery and decryption work without its
source configuration. Tests explicitly remove the source registry and confirm
that key replay fails while authenticated recovery/decryption succeeds. No manager
source changes, TestConsole changes, file I/O or network I/O are part of the adapter.
The caller configures KeyManager, provisions the signing public key and transports
the shares. Existing access to the original KeyManager remains another way to
obtain its keys; creating shares does not revoke that access.

## Mathematics and the three checks

Key replay returns exactly 16, 24 or 32 AES key bytes. Their meaning to AES or the
source that generated them does not change the sharing arithmetic.

Pedersen VSS encodes at most 31 bytes per Ristretto255 scalar. AES-128/192 keys
occupy one block; an AES-256 key occupies two. Each block has a sharing polynomial
`f(X) = s + a1*X + ... + a(k-1)*X^(k-1)` and a blinding polynomial r. Commitments
bind their coefficients. Verified interpolation recovers the scalar `s = f(0)`
and its exact original bytes. Both blocks of a 32-byte key use the same quorum.
See the [VSS mathematics](pedersen-vss.md) and
[coefficient-verification optimization](vss-verification-optimization.md).

Ed25519 authenticates the commitment packet and the context described below,
including the original handle and AES key length. The envelope also signs the
generation. Recipients supply their own trusted key and exact expectations.
See the [signature equations and envelope format](authenticated-commitment-packets.md).

AES-GCM checks the ciphertext with the recovered AES key. The existing artifact
layout remains `[nonce:12 | tag:16 | ciphertext]`; associated data remains
`[original handle:8 little endian | original additional associated data]`.
The raw decryptor authenticates that handle without replaying it. The same
additional associated data must be retained. A failed tag clears the plaintext
prefix, following the [platform decryption contract](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.decrypt?view=net-10.0).

These are separate checks: a signed commitment packet still needs valid VSS
shares; a recovered, verified key still needs the correct ciphertext, nonce,
tag, handle and associated data. The signature does not encrypt any metadata.

## Recovery context v1

The context is public and is placed inside the existing authenticated envelope.
Integers are little endian.

| Offset | Bytes | Contents |
| --- | --- | --- |
| 0 | 32 | ASCII `Mrbr.Cryptography.Vss.KeyManager` protocol domain |
| 32 | 8 | Original EncryptionManager key handle |
| 40 | 4 | AES key length: 16, 24 or 32 |
| 44 | 4 | Application-context length C |
| 48 | C | Arbitrary public application-context bytes |

The envelope version/suite is v1, pure Ed25519, as before. The original inner
commitment and share formats are unchanged. The adapter builds the expected
context locally during recovery; it does not trust identities taken from a
received packet. It additionally checks the authenticated inner secret length
before VSS can write key bytes, including for a packet signed by a valid dealer.

The application context could contain a backup identity or key identifier. Use
an unambiguous encoding for multiple fields and keep confidential data out of
it. Generation is supplied separately as an exact expected value. The caller
provides freshness policy and agreement on one common packet; no replay database,
clock, key rotation or distributed agreement is implemented here.

## Span APIs and buffer sizes

```csharp
int GetContextLength(int applicationContextLength);
int GetPacketLength(int keyLength, int threshold, int applicationContextLength);
int GetCreationWorkspaceLength(int keyLength, int threshold, int applicationContextLength);
int WriteContext(ulong originalHandle, int keyLength,
    ReadOnlySpan<byte> applicationContext, Span<byte> destination);

int CreateKeyShares(IKeyService keyService, ulong originalHandle, int keyLength,
    int threshold, int shareCount, ReadOnlySpan<byte> applicationContext,
    ulong generation, ReadOnlySpan<byte> signingSeed, Span<byte> sharesDestination,
    Span<byte> packetDestination, Span<byte> workspace);

bool TryReconstructKey(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet,
    ulong expectedOriginalHandle, int keyLength,
    ReadOnlySpan<byte> expectedApplicationContext, ulong expectedGeneration,
    ReadOnlySpan<byte> trustedPublicKey, Span<byte> destination,
    Span<byte> contextWorkspace, out int bytesWritten);

int ReconstructKey(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet,
    ulong expectedOriginalHandle, int keyLength,
    ReadOnlySpan<byte> expectedApplicationContext, ulong expectedGeneration,
    ReadOnlySpan<byte> trustedPublicKey, Span<byte> destination,
    Span<byte> contextWorkspace);
```

The adapter accepts AES key lengths 16/24/32 and `2 <= threshold <= count <= 255`.
Use the existing `PedersenVss.GetShareLength/GetSharesLength` helpers for shares.
Creation returns packed share bytes written; packet size comes from GetPacketLength.
Recovery context workspace needs GetContextLength bytes. All buffers belong to
the caller, so large application contexts do not cause growing stack allocations.

For threshold three and application-context length C:

| AES key bytes | VSS blocks | Each share | Envelope | Creation workspace |
| --- | ---: | ---: | ---: | ---: |
| 16 | 1 | 97 | 304+C | 204+C |
| 24 | 1 | 97 | 304+C | 212+C |
| 32 | 2 | 161 | 400+C | 316+C |

Each recovery context needs `48+C` bytes. Creation workspace holds the replayed
key, temporary inner commitments and signed context. Its entire used prefix is
wiped in finally, including after replay failure; its suffix is untouched.
Recovery workspace contains public context and its used prefix is also wiped.
Caller-owned recovered key bytes remain available until the caller clears them.

All full output/workspace spans must be mutually disjoint and disjoint from
every input, including unused suffixes. Input spans may overlap each other.
Disjoint slices of a common allocation work. Parameter errors precede replay
or writes. Replay failure leaves both output buffers unchanged. Failures after
share creation begins clear both used output prefixes. Untrusted recovery false
returns leave the key destination unchanged and bytesWritten zero. The throwing
wrapper reports those failures with CryptographicException. Output suffixes are
untouched. Every supplied share participates, including surplus shares.

## Four-step example

The following local demonstration starts with an existing EncryptionManager
ciphertext and handle, a separately protected signing seed and the matching
trusted public key. In deployment, the recipient obtains the trusted key and
expected metadata independently of the received shares/packet. It never needs
the signing seed or KeyManager. Public-key provisioning and private distribution
of the shares are outside this function.

```csharp
using System.Security.Cryptography;
using Mrbr.Cryptography.Vss;
using Mrbr.Cryptography.Vss.KeyManager;
using Mrbr.Service.KeyManager.Services;
using Mrbr.Service.EncryptionManager.Services;

static int ShareRecoverAndDecrypt(IKeyService keyService, ulong originalHandle,
    ReadOnlySpan<byte> cipher, ReadOnlySpan<byte> originalAdditionalData,
    ReadOnlySpan<byte> signingSeed, ReadOnlySpan<byte> trustedPublicKey,
    Span<byte> plaintextDestination, Span<byte> associatedDataWorkspace)
{
    const int keyLength = 32, threshold = 3, count = 5;
    const ulong generation = 7;
    ReadOnlySpan<byte> application = "example/key-42"u8;
    int shareLength = PedersenVss.GetShareLength(keyLength);
    Span<byte> shares = stackalloc byte[PedersenVss.GetSharesLength(keyLength, count)];
    Span<byte> selected = stackalloc byte[threshold * shareLength];
    Span<byte> packet = stackalloc byte[AuthenticatedKeySharing.GetPacketLength(keyLength, threshold, application.Length)];
    Span<byte> creationWorkspace = stackalloc byte[AuthenticatedKeySharing.GetCreationWorkspaceLength(keyLength, threshold, application.Length)];
    Span<byte> contextWorkspace = stackalloc byte[AuthenticatedKeySharing.GetContextLength(application.Length)];
    Span<byte> recoveredKey = stackalloc byte[keyLength];
    try
    {
        // Steps 1–2: the adapter replays into workspace, shares, signs and wipes workspace.
        AuthenticatedKeySharing.CreateKeyShares(keyService, originalHandle, keyLength,
            threshold, count, application, generation, signingSeed,
            shares, packet, creationWorkspace);

        // Local selection of coordinates 5, 2 and 4; normally collected from recipients.
        shares.Slice(4 * shareLength, shareLength).CopyTo(selected);
        shares.Slice(shareLength, shareLength).CopyTo(selected[shareLength..]);
        shares.Slice(3 * shareLength, shareLength).CopyTo(selected[(2 * shareLength)..]);

        // Step 3: receiver supplies independently trusted expectations; no KeyManager access.
        AuthenticatedKeySharing.ReconstructKey(selected, packet, originalHandle, keyLength,
            application, generation, trustedPublicKey, recoveredKey, contextWorkspace);

        // Step 4: import once and wipe the caller's recovered key immediately.
        using var decryptor = new AesGcmKeyDecryptor(recoveredKey);
        CryptographicOperations.ZeroMemory(recoveredKey);
        return decryptor.Decrypt(originalHandle, cipher, plaintextDestination,
            originalAdditionalData, associatedDataWorkspace);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(recoveredKey);
        CryptographicOperations.ZeroMemory(shares);
        CryptographicOperations.ZeroMemory(selected);
        CryptographicOperations.ZeroMemory(creationWorkspace);
        CryptographicOperations.ZeroMemory(contextWorkspace);
    }
}
```

The sample uses fixed small caller stack buffers. For arbitrary larger sizes,
reuse caller storage. The associated-data workspace needs
`AesGcmKeyDecryptor.GetAssociatedDataWorkspaceLength(originalAdditionalData.Length)`
bytes. Plaintext needs `cipher.Length - 28` bytes. The function intentionally
creates a fresh decryptor; retain a recovered-key context when repeatedly
decrypting artifacts encrypted under that same key. Dispose it when finished.

## Allocation boundary and validation

Replay/sharing/signing and key reconstruction allocate zero managed bytes after
initialization with the existing KeyService. Successful decryption using an
already initialized recovered-key context also allocates zero managed bytes.
The adapter has no per-call arrays, pool rentals, key objects or delegates.
Its scratch storage is supplied by the caller; existing VSS scratch stays bounded.

Constructing a new AesGcmKeyDecryptor imports a key and allocates managed/native
resources. A fresh recover/import/decrypt call therefore includes that allocation.
Benchmarks isolate initialization, reused decryption and fresh recovery/decryption
instead of presenting a reused context as importing a new key on every call.
Configuration, fixtures, caller buffers, initialization and exceptions are outside
the warm zero-allocation contract; native allocator traffic is not measured.

Release and Debug pass **522 tests**: the previous 460 plus **62 new cases**.
Coverage includes both real source types, all AES sizes, source-registry loss,
all three-of-five subsets, dense/sparse quorums and surplus shares, empty plaintext,
wrong signer/context/generation/handle/key length, signed inner-length mismatch,
corrupt shares/ciphertext/AAD, workspace wiping, failure-output/suffix behavior,
overlap rejection and warmed allocations. Builds have zero warnings/errors.

The standalone solution still runs 407 core/VSS/authentication tests. The optional
integration solution adds both manager integration suites. Run from the repository:

```powershell
./eng/Invoke-AuthenticatedRecovery.ps1 -Action Test -Configuration Release
./eng/Invoke-AuthenticatedRecovery.ps1 -Action Test -Configuration Debug
./eng/Invoke-AuthenticatedRecovery.ps1 -Action Benchmark
```

Use `-ServiceSourceRoot` when the sibling checkouts move. The wrapper passes
the same source-reference/configuration properties as the existing integration
script. The 48 benchmark cases cover Block/Matrix, AES-128/192/256 and two/three/
nine-share thresholds. Direct composition is compared with adapter creation;
recovery, reused decryption, initialization and fresh recovery/decryption are
measured separately. See the [recorded comparison](../benchmarks/results/2026-11-04-authenticated-recovery/README.md).
