# Sharing handles and key material

The optional `Mrbr.Cryptography.KeyManager` library adapts the existing span-based
`IKeyService` API. `Mrbr.Cryptography` itself remains data agnostic, dependency
free, and unchanged. EncryptionManager gains the separate
`Mrbr.Service.EncryptionManager.Services.AesGcmKeyDecryptor` class; its existing
service and handle-based decryption path are unchanged.

The optional [authenticated AES recovery adapter](authenticated-key-recovery.md)
now provides the complete Pedersen/Ed25519 workflow, binding the original handle,
AES key length, application context and generation to signed commitments.
That project is separate from the non-verifying adapter described here.

## Choose what recovery must restore

| Shared secret | Bytes per share | What recovery requires |
| --- | ---: | --- |
| `ulong` handle | 9 | Enough shares **and** the original KeyManager source bytes, mask, identity and replay settings |
| AES-128 key | 17 | Enough shares; no KeyManager source |
| AES-192 key | 25 | Enough shares; no KeyManager source |
| AES-256 key | 33 | Enough shares; no KeyManager source |

The handle is replay information, not a portable representation of the key. Both
Block and Matrix sources work with the adapter. KeyManager already supplies
`GetKey(ulong, Span<byte>)`, so no change to KeyManager is needed.

Key length, threshold, share grouping and cipher metadata belong to the calling
application. They are not encoded in the packed shares. Keep the original key
length, especially for Matrix replay. The workspace overload supports every
KeyManager key length from 1 to 65,535 bytes; this limit applies to the adapter,
not to the general sharing primitive.

If the handle itself is the secret being protected, storing that same handle in
plain text beside the ciphertext bypasses the sharing requirement for anyone
who can access the source configuration. Keep a cipher-only artifact for that
workflow rather than a `KeyHandleRaw`, `KeyHandleBase64`, or `KeyHandleHex` artifact.
For independent raw-key recovery, the original handle must instead be retained
as authenticated cipher metadata, as described below.

## Mathematics and encoding

For each input byte `s`, Shamir sharing independently chooses uniform random
coefficients and evaluates

```text
p(x) = s + a1*x + a2*x^2 + ... + a(k-1)*x^(k-1)
```

over GF(256). Addition and subtraction are XOR; multiplication reduces modulo
the existing field polynomial `0x11B`. A handle is first encoded as eight bytes
in little-endian order. The same eight polynomial calculations then apply; no
special arithmetic or weaker random coefficients are introduced for handles.

Recovery computes the public interpolation weights once for the supplied
coordinates and applies them to every byte:

```text
lambda_i = product(j != i) x_j / (x_i XOR x_j)
s        = XOR_i lambda_i * share_i
```

Changing from a 32-byte key to an eight-byte handle reduces payload work and
storage. It does not remove the interpolation-weight work, which depends on the
number of supplied shares. The subsequent [weight optimization](interpolation-weights.md)
reduces that setup cost using exact coordinate-only logarithm tables.
Use the original threshold even when supplying more than that many shares.

## Create shares

```csharp
using Mrbr.Cryptography.KeyManager;

// keyService is the application's existing IKeyService; handle already exists.
Span<byte> handleShares = stackalloc byte[9 * 5];
ShamirKeySharing.CreateHandleShares(handle, threshold: 3, shareCount: 5, handleShares);

// Alternatively share the actual 32-byte key, without an intermediate byte[].
Span<byte> keyShares = stackalloc byte[33 * 5];
ShamirKeySharing.CreateKeyShares(keyService, handle, keyLength: 32,
    threshold: 3, shareCount: 5, keyShares);
```

Both methods generate fresh randomized shares on each call. Coordinates and the
packed `[coordinate | payload]` format are exactly those of the core API.
The key-creation overload above uses bounded stack scratch space for keys up to
256 bytes, including all AES sizes. For larger keys, supply a separate
`Span<byte> keyWorkspace` as the final argument. Its used prefix is wiped in a
`finally` block, including after a replay failure; its suffix remains untouched.
All parameters are validated before replay or buffer changes. Scratch space and
output must not overlap, including unused suffixes.

## Recover and replay a handle

```csharp
// selectedHandleShares contains complete nine-byte shares in any order.
ulong recoveredHandle = ShamirKeySharing.ReconstructHandle(selectedHandleShares, 3);
Span<byte> key = stackalloc byte[32];
try
{
    keyService.GetKey(recoveredHandle, key);
    // Use the key while it is in scope.
}
finally
{
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
}
```

## Recover key material and decrypt without KeyManager

Existing EncryptionManager cipher bytes are `[12-byte nonce | 16-byte tag |
ciphertext]`. Encryption authenticates the concatenation
`[8-byte little-endian original handle | additional associated data]`. Consequently
raw-key recovery still needs the **original handle value** and the same additional
associated data. The raw decryptor authenticates that value without replaying it
or consulting KeyManager.

```csharp
using System.Security.Cryptography;
using Mrbr.Cryptography.KeyManager;
using Mrbr.Service.EncryptionManager.Services;

Span<byte> recoveredKey = stackalloc byte[32];
try
{
    ShamirKeySharing.ReconstructKey(selectedKeyShares, keyLength: 32,
        threshold: 3, recoveredKey);
    using var decryptor = new AesGcmKeyDecryptor(recoveredKey);
    CryptographicOperations.ZeroMemory(recoveredKey); // Imported by the context.

    // plaintext is caller-owned and at least cipher.Length - 28 bytes.
    int written = decryptor.Decrypt(originalHandle, cipher, plaintext, originalAdditionalData);
    // Reuse this context for further artifacts encrypted with this same key.
}
finally
{
    CryptographicOperations.ZeroMemory(recoveredKey);
}
```

The convenience overload supports up to 248 bytes of additional associated data
using at most 256 stack bytes. For larger data, pass caller-owned scratch space:

```csharp
int scratchLength = AesGcmKeyDecryptor.GetAssociatedDataWorkspaceLength(originalAdditionalData.Length);
// Obtain scratchLength bytes from the application's existing storage.
decryptor.Decrypt(originalHandle, cipher, plaintext, originalAdditionalData, associatedDataWorkspace);
```

The used scratch prefix is wiped, and destination suffixes are untouched. Overlaps
involving the output or scratch space are rejected; read-only inputs may overlap
each other. Disjoint slices of one backing buffer are allowed. Validation errors
leave buffers unchanged. On authentication
failure, the plaintext prefix and used scratch prefix are cleared and the
cryptographic exception propagates. This agrees with the
[platform's authenticated decryption contract](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.decrypt?view=net-10.0).

Shamir shares alone do not authenticate recovered bytes. Modified shares can
reconstruct a wrong key; AES-GCM then rejects the ciphertext. This does not make
the shares verifiable and does not identify which share was damaged.

## Allocation boundary

Successful adapter creation, reconstruction and replay calls allocate zero
managed bytes after runtime/RNG initialization when using the existing concrete
KeyService. Successful decryptions using an already initialized raw-key context
also allocate zero managed bytes on the measured Windows/.NET 11 runtime. Large
associated data uses caller-owned scratch space and never silently falls back
to a heap buffer.

Creating an `AesGcmKeyDecryptor` imports a key and allocates managed/native
resources. Reconstructing a key and constructing a fresh context for every
decryption therefore is **not** an end-to-end zero-allocation operation. Context
initialization and the full recover/initialize/decrypt path are separate benchmark
cases. Dispose contexts when finished, use each instance serially, and wipe
caller-owned recovered key bytes and plaintext when they are no longer needed.
Failure paths can allocate exceptions. Configuration acceptance, test fixtures,
benchmark setup and caller-owned buffer creation are outside steady-state counts.

## Build, test and benchmark the source integration

The original `Mrbr.Cryptography.slnx` remains independently buildable. The
additional `Mrbr.Cryptography.Integration.slnx` includes the adapter, tests and
benchmarks referencing the checked-out manager sources. By default these live
under `C:/dev/source`, alongside their existing source-generator and utility
project dependencies.

Run from the Mrbr.Cryptography repository:

```powershell
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug
./eng/Invoke-Integration.ps1 -Action Benchmark -Artifacts 'BenchmarkDotNet.Artifacts/integration'
```

Use `-ServiceSourceRoot D:/other/source` when the manager checkouts move. The
script supplies `UseLocalKeyManager=true` to select EncryptionManager's existing
source-reference option and `ShouldUnsetParentConfigurationAndPlatform=false` to
preserve Release/Debug across the external project graph. The benchmark job
passes these same properties to its generated build. SDK RC3 is selected from
this repository; the manager SDK pins are not modified.

There are 53 integration cases, including dense/sparse coordinates, real Block
and Matrix replay, all AES key sizes, large keys, registry loss, associated-data
boundaries, corruption, overlap rejection, cleanup and allocation measurements.
The original 144 core cases run alongside them. The 49 benchmarks use real RNG
and real KeyService replay, three-of-five sharing, 8-byte handles and 16/24/32-byte
keys. Decryption comparisons use 1 KiB plaintext and no additional associated
data. Setup validates recovered results before timing.

See the [recorded integration results](../benchmarks/results/2026-11-03-integration/README.md)
for timings, allocation boundaries and reproducibility details.
