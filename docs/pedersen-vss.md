# Pedersen verifiable secret sharing

`Mrbr.Cryptography.Vss.PedersenVss` creates shares of arbitrary bytes, lets each
recipient verify its own share, and reconstructs only after every supplied share
passes verification. The separate `Mrbr.Cryptography.Vss` project has no reference
to the existing `Mrbr.Cryptography` project. No existing Shamir source, arithmetic,
dispatch, public API or project dependency is changed.

This implementation follows the commitment-based construction in
[Pedersen's VSS paper](https://cgi.di.uoa.gr/~aggelos/crypto/page8/assets/Pedersen-VSS.PDF).
It is a local cryptographic primitive. The calling application distributes shares
privately and establishes one authenticated, common public commitment packet.
File, network, console and key-manager I/O remain outside this library.

The optional [Ed25519 authentication project](authenticated-commitment-packets.md)
provides a signed envelope for this common packet, binding application context
and generation to a caller-trusted public key. This VSS project does not reference
that layer, and its share/commitment formats remain unchanged.

## A different field and share format

The existing fast implementation works over GF(256), where addition is XOR.
Pedersen commitments instead use the scalar field of a prime-order group. Here
that group is [Ristretto255, specified in RFC 9496](https://www.rfc-editor.org/rfc/rfc9496.html),
with order

```text
l = 2^252 + 27742317777372353535851937790883648493
```

Scalar addition, subtraction, multiplication and division are integer operations
modulo `l`. Subtraction is **not XOR**. Group points use elliptic-curve addition;
`a*G` denotes scalar multiplication of point `G` by scalar `a`.

GF(256) shares cannot be passed to these VSS APIs. Create VSS shares from the
original bytes. The new implementation has its own wire format and arithmetic,
so the non-verifying implementation keeps its existing performance.

Every block encodes at most **31 bytes** as an unsigned little-endian scalar.
Its value is below `2^248`, hence below `l`, making encoding injective without
reducing or losing input information. The final block is zero-padded; original
length is retained in the public header, preserving trailing zero bytes.
A full arbitrary 32-byte value cannot be reduced modulo `l` reversibly, so a
32-byte key requires two blocks.

## Two polynomials and a commitment to each coefficient

For each data block `s`, choose two independent degree-at-most-`k-1` polynomials:

```text
f(x) = s  + a1*x + ... + a(k-1)*x^(k-1)
r(x) = b0 + b1*x + ... + b(k-1)*x^(k-1)
```

All random coefficients are sampled uniformly from the **entire** field,
including zero. Rejection sampling draws 253 random bits and accepts only values
below `l`. It does not introduce reduction bias or exclude zero. New independent
coefficients are generated for every block and every sharing operation.

Let `G` be the standard Ristretto generator. A second generator is derived as

```text
H = RistrettoFromHash(SHA512("Mrbr.Cryptography.PedersenVss/v1/H/ristretto255"))
```

The public domain string makes this derivation reproducible. It maps a hash to a
group element rather than multiplying `G` by a publicly known scalar. Binding
requires that no participant knows a scalar `d` for which `H = d*G`.

For `a0 = s`, publish these `k` points:

```text
C_j = a_j*G + b_j*H
```

Each commitment hides its coefficient because the independently uniform blinding
scalar masks it across the group. A recipient at coordinate `x` privately receives
the pair

```text
y = f(x),  z = r(x)
```

The random polynomial coefficients and creation scratch are wiped after use.
Both components of each share are private application data.

H's same encoding is now stored as immutable assembly data and independently
rederived in tests, avoiding repeated hash-to-group work. The wire value is unchanged.

## How verification works

The recipient checks

```text
y*G + z*H  ==  C_0 + x*C_1 + x^2*C_2 + ... + x^(k-1)*C_(k-1)
```

Expanding the right side and collecting terms gives `f(x)*G + r(x)*H`, which is
the left side for a valid share. The recipient can check this relation without
learning the secret constant or the private polynomial coefficients. Point
Horner evaluation computes the right side without separately constructing every
power of `x`.

Public coordinates 1–6 and 8 now use short addition chains where measured cheaper
than full native multiplication. Other coordinates retain the native path.

The implementation checks canonical scalar encodings and valid canonical group
points. Zero scalars and identity points are mathematically legitimate and are
supported. Because the native scalar-multiplication API rejects zero, the wrapper
uses a mask to replace a zero scalar with one for the native call, then selects
the encoded identity. It does not rely on output from a failed native call.
The identity-point shortcut depends only on public commitment-derived points.

`VerifyShare` returns false on invalid framing, a mismatched commitment digest,
out-of-range coordinates, noncanonical values or a failed group equation.
It validates one share independently; no quorum is needed for that check.

## Reconstruction and ordinary prime-field interpolation

Given `m >= k` distinct, verified coordinates, reconstruct each scalar at zero:

```text
w_i = product(j != i) x_j / (x_j - x_i)    modulo l
s   = sum(i) w_i*y_i                      modulo l
```

For coordinates `1` and `2`, the weights are `2` and `-1 mod l`, so
`s = 2*y_1 - y_2`. This differs from the GF(256) example because the fields have
different addition and subtraction rules. The same interpolation principle is
used; its arithmetic must match the field in which the polynomials were created.

Reconstruction uses **all supplied shares**, verifies every one, and rejects
duplicate coordinates. It does not silently discard invalid extras beyond the
threshold. Verification does not itself prove that the committed constant fits
the byte encoding. For example, a dealer can commit to scalar `256` while claiming
a one-byte message. Reconstruction checks all recovered padding before writing
any output and rejects that case.

For secrets up to 62 bytes and two through nine supplied shares, reconstruction
instead interpolates the full private polynomials and checks their coefficient
commitments. This is exactly equivalent to the original evaluation equations.
The result fits bounded stack scratch and is copied only after all checks pass.
Larger cases retain the original verify/check/interpolate-again structure. See the
[optimization guide](vss-verification-optimization.md) for the proof, surplus-share
handling and implementation details.

## Public API

```csharp
int GetShareLength(int secretLength);
int GetSharesLength(int secretLength, int shareCount);
int GetCommitmentsLength(int secretLength, int threshold);
bool TryReadParameters(ReadOnlySpan<byte> commitments,
    out int secretLength, out int threshold, out int shareCount);

int CreateShares(ReadOnlySpan<byte> secret, int threshold, int shareCount,
    Span<byte> sharesDestination, Span<byte> commitmentsDestination);
bool VerifyShare(ReadOnlySpan<byte> share, ReadOnlySpan<byte> commitments);
bool TryReconstruct(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> commitments,
    Span<byte> destination, out int bytesWritten);
int Reconstruct(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> commitments,
    Span<byte> destination);
```

Counts satisfy `2 <= threshold <= shareCount <= 255`. The one-byte coordinate
format sets this limit; it is not the mathematical limit of the prime field.
Empty input is supported. Sizing is checked for integer overflow.

`TryReadParameters` parses framing for buffer sizing; it does not authenticate
the packet or verify points. Full verification happens in `VerifyShare` and
reconstruction. Pass exact populated prefixes, excluding unused capacity.

## Example: three of five

```csharp
using System.Security.Cryptography;
using Mrbr.Cryptography.Vss;

Span<byte> secret = stackalloc byte[32];
RandomNumberGenerator.Fill(secret); // Or use any caller-provided data.
const int threshold = 3, count = 5;
int shareLength = PedersenVss.GetShareLength(secret.Length);
Span<byte> shares = stackalloc byte[PedersenVss.GetSharesLength(secret.Length, count)];
Span<byte> commitments = stackalloc byte[PedersenVss.GetCommitmentsLength(secret.Length, threshold)];
Span<byte> selected = stackalloc byte[shareLength * threshold];
Span<byte> recovered = stackalloc byte[secret.Length];
try
{
    PedersenVss.CreateShares(secret, threshold, count, shares, commitments);
    // The application privately distributes individual shares and publishes
    // this same commitment packet through an authenticated common channel.
    shares.Slice(4 * shareLength, shareLength).CopyTo(selected);
    shares[..shareLength].CopyTo(selected[shareLength..]);
    shares.Slice(2 * shareLength, shareLength).CopyTo(selected[(2 * shareLength)..]);

    bool valid = PedersenVss.VerifyShare(selected[..shareLength], commitments);
    bool recoveredSuccessfully = PedersenVss.TryReconstruct(selected, commitments, recovered, out int written);
    // On success, recovered[..written] contains the original data.
}
finally
{
    CryptographicOperations.ZeroMemory(secret);
    CryptographicOperations.ZeroMemory(shares);
    CryptographicOperations.ZeroMemory(selected);
    CryptographicOperations.ZeroMemory(recovered);
}
```

For larger buffers, obtain storage in the application and pass spans; do not
scale stack allocations with unbounded input. The library's own scratch remains
bounded by the 255-share maximum, independent of payload size.

## Wire version 1

Let `B = ceil(secretLength / 31)` and `k = threshold`.

The common public commitment packet is **44 + 32*B*k** bytes:

| Offset | Size | Contents |
| --- | ---: | --- |
| 0 | 4 | ASCII `MVSS` |
| 4 | 1 | Wire version `1` |
| 5 | 1 | Pedersen/Ristretto255 suite `1` |
| 6 | 1 | Threshold |
| 7 | 1 | Total shares |
| 8 | 4 | Original byte length, unsigned little endian |
| 12 | 32 | Random share-set identifier |
| 44 | 32*B*k | Block-major coefficient commitments `C0..C(k-1)` |

Each private share is **33 + 64*B** bytes:

| Offset | Size | Contents |
| --- | ---: | --- |
| 0 | 32 | SHA-256 of the entire exact commitment packet |
| 32 | 1 | Nonzero coordinate, at most the committed total count |
| 33 | 64*B | Block-major `(y,z)` scalar pairs, each scalar canonical 32-byte little endian |

| Original bytes | Bytes per share | Commitments for three-of-five |
| --- | ---: | ---: |
| 0 | 33 | 44 |
| 8 / 16 / 24 / 31 | 97 | 140 |
| 32 / 62 | 161 | 236 |
| 1,024 | 2,209 | 3,308 |

The digest binds a share to a particular packet for comparison; it is not a
signature or dealer authentication. Replacing both the shares and packet can
produce another valid sharing. All recipients need the same authenticated packet
to prevent dealer equivocation. Binding relies on the discrete-log assumption;
it is not a post-quantum binding scheme. Verification establishes consistency,
not that a dealer chose the application's intended secret or used honest entropy.
Distributed complaint resolution and agreement belong to the application protocol.

## Allocation, cleanup and backend

The new project pins **libsodium 1.0.22**, supplied by its native NuGet implementation
package under the [ISC license](https://www.nuget.org/packages/libsodium/1.0.22).
It supplies a narrow `LibraryImport` binding for the
[Ristretto primitives](https://libsodium.gitbook.io/doc/advanced/point-arithmetic/ristretto)
and native SHA-256/SHA-512. It does not implement curve arithmetic from scratch.
Only VSS consumers acquire this dependency; consumers of the original class
library do not. The tested host is Windows x64; other runtime/architecture assets
are packaged, but were not executed here. Windows requires the corresponding
Visual C++ runtime, normally present with the SDK/Visual Studio.

Successful warmed calls allocate **zero managed bytes** with caller-owned spans.
Non-throwing untrusted-data rejection is also checked for zero managed allocations.
Native library loading, libsodium/RNG/runtime initialization, caller storage and
exception objects are outside this contract. MemoryDiagnoser measures managed
allocations; it does not measure native allocator traffic.

Creation uses at most 16,320 bytes for the two coefficient arrays, plus bounded
scalar/point scratch and native call frames. Reconstruction uses at most 8,160
bytes for public interpolation weights, plus coordinates, duplicate tracking,
scalar/point scratch and native frames. Both totals are independent of data length.
No pool, heap cache or global mutable verification state is used. Scratch never
grows with unbounded payload length.

The small-quorum path additionally uses at most 2,592 bytes for its public basis
matrix, 576 private coefficient bytes and 62 plaintext bytes, plus bounded public
matrix-construction scratch. Those fixed limits are independent of larger inputs,
which select the original fallback. Private scratch is wiped before returning.

Input and creation destinations must be disjoint, including unused suffixes.
Reconstruction's full destination must be disjoint from both read-only inputs.
All buffers must remain stable for the duration of a call. Parameter errors
precede output writes. Creation exceptions wipe both used output prefixes;
scratch holding coefficients or secret intermediates is cleared in `finally`.
`TryReconstruct` returns false with zero written bytes and unchanged destination
on malformed input, insufficient quorum, verification failure or invalid decoded
padding. `Reconstruct` wraps those failures in `CryptographicException`.
Unexpected backend exceptions during output clear the plaintext prefix before
propagating. Successful calls preserve destination suffixes.

VSS has substantially more computation and storage than non-verifying sharing:
each block adds curve commitments and verification. It accepts arbitrary data,
but runtime scales with the number of 31-byte blocks. The benchmarks measure
small keys/handles and a 1 KiB payload so that cost is visible.

## Tests and benchmarks

Release and Debug each pass **460 tests**: the unchanged 228 core tests,
129 VSS tests, 50 optional authentication tests and the existing 53 manager
integration tests. VSS tests cover
RFC 9496 group/hash-to-group vectors, independent BigInteger scalar arithmetic
and interpolation, exact sampling boundaries, zero/identity values, chunk edges,
trailing zeros, all three-of-five subsets/permutations, sparse coordinates up to
255, maximum threshold, payload/blinding/point/metadata tampering, mixed sets,
duplicate/insufficient shares, invalid byte encoding, RNG cleanup, buffer suffixes
and warmed allocations.

This protocol implementation has not undergone an independent cryptographic audit;
the primitive vectors, independent scalar oracle and tests describe the validation
performed here.

```powershell
dotnet build Mrbr.Cryptography.slnx -c Release
dotnet test --solution Mrbr.Cryptography.slnx -c Release --no-build
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug

dotnet run --project benchmarks/Mrbr.Cryptography.Vss.Benchmarks -c Release -- --filter '*' --launchCount 1 --warmupCount 20 --iterationCount 15 --iterationTime 150 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/vss
```

The standalone solution runs 407 core/VSS/authentication tests; the integration script includes
the additional 53 tests against the checked-out managers. See the
[recorded VSS benchmark report](../benchmarks/results/2026-11-03-vss/README.md)
for timings, throughput, allocation counts, source fingerprints and comparisons
with the unchanged non-verifying implementation.
