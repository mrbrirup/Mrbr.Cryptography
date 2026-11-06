# Authenticated commitment packets

Reference the separate .NET 11 project `Mrbr.Cryptography.Vss.Authentication` to
use `Mrbr.Cryptography.Vss.Authentication.AuthenticatedCommitmentPacket`.
It references the existing VSS project and pins the same native libsodium 1.0.22
package. Neither existing library references the authentication project. Existing
Shamir and Pedersen source, APIs and share/commitment wire bytes are unchanged.

The new layer provides caller-buffer APIs for signing-seed generation, public-key
derivation, envelope creation/authentication, authenticated share verification
and authenticated reconstruction. It performs no I/O, key storage or trust discovery.

## Why authenticate commitments?

Pedersen verification checks a share against a commitment packet. The digest in
the share associates it with that packet. Replacement shares and a replacement
packet can form another valid sharing. A signature makes that substitution
detectable to recipients holding the intended dealer's trusted public key.

Provision that public key independently: the envelope contains no public key that
could silently become a trust anchor. Keep signing seeds separate from encryption
keys/data being shared. Recipients need the public key, not the signing seed.
Authentication establishes the signing key and exact bytes. VSS establishes share
consistency. The caller establishes expected context/generation and agreement on
one common packet. A signer can still issue different packets for the same
context/generation; signatures do not implement agreement between recipients or
prove the dealer shared the intended data. See the [VSS protocol boundaries](pedersen-vss.md).
This implementation has not undergone an independent cryptographic audit.

## Ed25519 mathematics

The Edwards25519 base point B has prime order
`l = 2^252 + 27742317777372353535851937790883648493`.
`[a]B` means adding B to itself a times.

1. SHA-512 expands a 32-byte seed. Ed25519 prunes the lower half into secret
   scalar a and retains the upper half as a nonce prefix. Public key `A = [a]B`
   is encoded in 32 bytes.
2. For message M, `r = SHA512(prefix || M) mod l` and `R = [r]B`.
3. `h = SHA512(encode(R) || encode(A) || M) mod l`, then `S = (r + h*a) mod l`.
   The signature `encode(R) || encode(S)` occupies 64 bytes.
4. The underlying verification equation is `[S]B = R + [h]A`, because
   `[r + h*a]B = [r]B + [h]([a]B)`.

Libsodium performs the curve arithmetic and encoding checks. See
[RFC 8032 section 5.1](https://www.rfc-editor.org/rfc/rfc8032.html#section-5.1)
and [libsodium's detached-signature API](https://doc.libsodium.org/public-key_cryptography/public-key_signatures).
M is our complete contiguous envelope prefix, excluding only its signature.
This is pure Ed25519; application context is included in M. Ed25519ctx and
Ed25519ph are separate signature variants and are not used. Ristretto255 VSS
points and Ed25519 public keys retain their own encodings and roles.

## Envelope v1

Integers are little endian. No padding or trailing capacity is accepted.

| Offset | Bytes | Contents |
| --- | --- | --- |
| 0 | 32 | ASCII `Mrbr.Cryptography.Vss.AuthPacket` domain |
| 32 | 1 | Envelope version: 1 |
| 33 | 1 | Signature suite: 1, pure Ed25519 |
| 34 | 2 | Reserved, both zero |
| 36 | 4 | Context length C, unsigned |
| 40 | 4 | Commitment packet length P, unsigned |
| 44 | 8 | Generation number |
| 52 | C | Public application-context bytes |
| 52+C | P | Exact existing Pedersen commitment packet |
| 52+C+P | 64 | Signature over every preceding byte |

Size is `116 + C + P`. The domain identifies this protocol; explicit lengths
make the transcript unambiguous. The inner packet's threshold, count, secret
length, VSS version/suite and random set ID are covered too. Private shares keep
their existing format.

For a 32-byte secret at threshold three, P is `44 + 2*3*32 = 236` bytes.
With 16 context bytes the envelope is 368 bytes; each private share stays 161
bytes. One common public envelope can accompany all shares. Context is public;
use an unambiguous application encoding and keep confidential data out of it.

## Span APIs and contracts

```csharp
int GetPacketLength(int commitmentsLength, int contextLength);
void GenerateSigningSeed(Span<byte> destination);
void DerivePublicKey(ReadOnlySpan<byte> signingSeed, Span<byte> destination);

int Create(ReadOnlySpan<byte> commitments, ReadOnlySpan<byte> context,
    ulong generation, ReadOnlySpan<byte> signingSeed, Span<byte> destination);

bool TryVerify(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> expectedContext,
    ulong expectedGeneration, ReadOnlySpan<byte> trustedPublicKey,
    out ReadOnlySpan<byte> commitments);

bool VerifyShare(ReadOnlySpan<byte> share, ReadOnlySpan<byte> packet,
    ReadOnlySpan<byte> expectedContext, ulong expectedGeneration,
    ReadOnlySpan<byte> trustedPublicKey);

bool TryReconstruct(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet,
    ReadOnlySpan<byte> expectedContext, ulong expectedGeneration,
    ReadOnlySpan<byte> trustedPublicKey, Span<byte> destination,
    out int bytesWritten);

int Reconstruct(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet,
    ReadOnlySpan<byte> expectedContext, ulong expectedGeneration,
    ReadOnlySpan<byte> trustedPublicKey, Span<byte> destination);
```

`SigningSeedLength` and `PublicKeyLength` are 32, `SignatureLength` is 64 and
`HeaderLength` is 52. Seed/public-key inputs must be exactly 32 bytes. Key output
methods fill the first 32 bytes, leaving suffixes untouched.

`Create` checks inner framing through `PedersenVss.TryReadParameters`, then copies
and signs the packet. It does not check all group points or dealer honesty.
`TryVerify` checks exact framing, expected context/generation, inner framing and
signature. Success returns a slice of the envelope without copying; false leaves
that out span default. Group validation and share consistency are checked by the
VSS methods. Authenticated wrappers perform both layers in order.

Keep all inputs stable for the entire call. A returned verified slice must retain
live, unchanged backing storage throughout later use. An old authentication result
does not authenticate subsequent mutations of its backing buffer.

Untrusted-data false returns leave destination unchanged and bytesWritten zero.
The throwing reconstruction wrapper reports such failures with
`CryptographicException`. Caller errors, including invalid key lengths, small
destinations and overlapping input/output spans, throw before output writes.
All supplied shares participate; invalid extras fail reconstruction. Successful
calls leave suffixes untouched.

Creation inputs must be disjoint from the full destination, including unused
suffixes. Reconstruction enforces this for every input, including trusted key
and expected context. Input spans may overlap each other; disjoint slices of a
common allocation work. Sizing uses checked arithmetic. Untrusted wire lengths
use wide arithmetic before int conversion or slicing.

## Workflow and freshness

1. The dealer holds a signing seed and independently provisions its public key
   to recipients using the application's trust mechanism.
2. Create shares/commitments with `PedersenVss.CreateShares`.
3. Sign an envelope with application context and a chosen generation.
4. Privately distribute shares and distribute the same public envelope.
5. Recipients supply trusted key, expected context and exact generation to
   `VerifyShare` or `TryReconstruct`.
6. Buffer owners clear seeds, shares and plaintext when finished.

Context could identify an application and key; generation could identify a backup
version. Generation is authenticated metadata, not an automatic clock or counter.
The caller retains/obtains the expected value independently. An older envelope
fails when a newer generation is expected, but still verifies when its old
generation is supplied explicitly. The library has no persistence or replay state.
Signatures do not encrypt the packet or the private shares.

This small local example uses bounded caller stack buffers. In deployment, the
recipient gets its trusted public key and expectations independently of received
data, and never receives the signing seed.

```csharp
using System.Security.Cryptography;
using Mrbr.Cryptography.Vss;
using Mrbr.Cryptography.Vss.Authentication;

const int threshold = 3, shareCount = 5;
const ulong generation = 7;
ReadOnlySpan<byte> context = "example/key-42"u8;
Span<byte> seed = stackalloc byte[32], publicKey = stackalloc byte[32];
Span<byte> secret = stackalloc byte[32], recovered = stackalloc byte[32];
int shareLength = PedersenVss.GetShareLength(secret.Length);
Span<byte> shares = stackalloc byte[PedersenVss.GetSharesLength(secret.Length, shareCount)];
Span<byte> selected = stackalloc byte[threshold * shareLength];
Span<byte> commitments = stackalloc byte[PedersenVss.GetCommitmentsLength(secret.Length, threshold)];
Span<byte> envelope = stackalloc byte[AuthenticatedCommitmentPacket.GetPacketLength(commitments.Length, context.Length)];
try
{
    // Dealer; seed protection and public-key provisioning belong to the application.
    AuthenticatedCommitmentPacket.GenerateSigningSeed(seed);
    AuthenticatedCommitmentPacket.DerivePublicKey(seed, publicKey);
    RandomNumberGenerator.Fill(secret);
    PedersenVss.CreateShares(secret, threshold, shareCount, shares, commitments);
    AuthenticatedCommitmentPacket.Create(commitments, context, generation, seed, envelope);

    // Select coordinates 2, 4 and 5 for a local recovery demonstration.
    shares.Slice(shareLength, shareLength).CopyTo(selected);
    shares.Slice(3 * shareLength, shareLength).CopyTo(selected[shareLength..]);
    shares.Slice(4 * shareLength, shareLength).CopyTo(selected[(2 * shareLength)..]);

    // Recipient: publicKey/context/generation are trusted expectations here.
    if (!AuthenticatedCommitmentPacket.TryReconstruct(selected, envelope, context,
        generation, publicKey, recovered, out int written) || written != secret.Length ||
        !secret.SequenceEqual(recovered))
        throw new CryptographicException("Authenticated recovery failed.");
}
finally
{
    CryptographicOperations.ZeroMemory(seed);
    CryptographicOperations.ZeroMemory(secret);
    CryptographicOperations.ZeroMemory(recovered);
    CryptographicOperations.ZeroMemory(shares);
    CryptographicOperations.ZeroMemory(selected);
}
```

Reuse caller storage for larger data rather than stackalloc unbounded buffers.
No TestConsole project is created or modified by this increment.

## Allocation and backend boundaries

Creation uses its caller destination as the contiguous signed message and needs
only 64 bytes of expanded secret key plus a 32-byte public-key temporary on the
stack. The secret key is wiped in finally. Public-key derivation wipes its 64-byte
secret-key temporary too. Creation backend exceptions clear the used output prefix.
There are no per-call arrays, key objects, delegates, pool rentals or managed
signature states. Verification returns an existing slice. Reconstruction retains
the VSS implementation's existing bounded scratch behavior.

Warmed successful operations and untrusted-data false returns allocate zero
managed bytes on the tested runtime. Runtime/native-library/RNG initialization,
caller buffers and exceptions are outside that contract. Native allocator traffic
is not measured by managed allocation tests or MemoryDiagnoser. The tested host is
Windows x64; other packaged architectures retain the earlier VSS validation limits.

## Validation and measurements

There are **50 new tests**. Release and Debug each pass **460 tests**: 228 core,
129 VSS, 50 authentication and 53 manager integration. Both builds have zero
warnings. The standalone solution runs 407 tests.

Coverage includes three RFC 8032 vectors checked with the native backend and a
separate test-only BigInteger signer; exact envelope transcripts checked with that
signer; every one-byte mutation and every truncation of a representative envelope;
wrong signers, contexts/generations and unsigned changes matching new expectations;
whole-set substitution; signed invalid VSS points; corrupt surplus shares and
duplicate/insufficient quorums; noncanonical signature scalars and invalid public
keys; length overflow and random malformed packets; overlap/suffix/failure-output
behavior; zero managed allocations on warm success and false returns.

Eight benchmark scenarios cover 8/32/62/1024-byte secrets, two/three/five/nine
shares and dense/sparse coordinates. Six methods measure envelope creation,
authentication, plain/authenticated share verification and plain/authenticated
reconstruction: 48 cases. Paired methods receive identical inputs and setup/buffer
allocation are outside measurement. See the [recorded results](../benchmarks/results/2026-11-04-vss-authenticated/README.md).

```powershell
dotnet test --solution Mrbr.Cryptography.slnx -c Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug

dotnet run --project benchmarks/Mrbr.Cryptography.Vss.Authentication.Benchmarks -c Release -- --filter '*' --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 120 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/vss-authenticated
```
