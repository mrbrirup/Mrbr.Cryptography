# Faster Pedersen verification and reconstruction

This increment preserves the VSS API, wire version, scalar field, group, generator
derivation, randomness distribution and acceptance equations. It changes how
those equations are computed. The non-verifying `ShamirSharedSecret` project is
untouched.

There are three optimizations: immutable storage for the fixed generator H,
short multiplication chains for selected public coordinates, and coefficient
verification for small reconstruction quorums. None uses a probabilistic batch
check or a cache of private data.

## Measure the expensive primitive first

A bounded diagnostic on this Windows x64 host measured encoded Ristretto point
addition at approximately 23.64 us, full point multiplication at 92–101 us, and
the fixed H derivation at 25 us. This motivated chains of at most three encoded
additions. Chains requiring four or more were not a reliable improvement.
The subsequent paired BenchmarkDotNet run measures complete public operations;
these diagnostic numbers are not the final speedup estimates.

## Store the same public generator

The previous implementation repeated

```text
H = RistrettoFromHash(SHA512("Mrbr.Cryptography.PedersenVss/v1/H/ristretto255"))
```

on every operation. H is fixed public protocol data. Its encoding is now stored
as a 32-byte `ReadOnlySpan<byte>` literal in assembly data:

```text
06de12f5f125af7742d71576de0e2d7c8575204eaf1645129e06e6e33ebb326c
```

The existing primitive test independently recomputes SHA-512 and hash-to-group
and checks equality. Deterministic creation is also checked byte-for-byte against
the frozen original implementation. This is the same H, with no change to the
unknown-discrete-log assumption or persisted commitments. No managed array or
mutable generator cache is created.

## Use short chains only for public coordinates

The right side of a share-verification equation is evaluated with point Horner:

```text
R = C_(k-1)
R = x*R + C_j          for j = k-2 down to 0
```

Coordinates are public one-byte integers. Multiplication by 1 is a copy;
multiplication by 2 is one doubling; multiplication by 5 can be computed as

```text
2P = P + P
4P = 2P + 2P
5P = 4P + P
```

The new helper uses at most three encoded additions for coordinates **1–6 and 8**.
Coordinates 7, 9 and 10–255 retain full native multiplication. Identity-point
handling is preserved. Tests compare every coordinate 0–255 against full native
multiplication for the generator, H, another public point and the identity.

The branch and addition-chain bits depend only on the public coordinate, and
the multiplied point is derived only from public commitments. Private values,
blinding scalars and private interpolated coefficients still use the original
native full-width multiplication with zero-scalar masking. This public-coordinate
helper is not a general replacement for secret scalar multiplication.

The cutoff is a performance choice for this host. Other architectures may have
different relative costs; correctness does not depend on that cutoff.

## Change the basis of the verification equations

Original reconstruction checks, for each supplied share i,

```text
y_i*G + z_i*H = sum(j=0..k-1) x_i^j*C_j
```

For `m` distinct supplied coordinates, interpolate the complete degree-at-most-
`m-1` polynomials through the private values:

```text
F(X) = sum(i) y_i*L_i(X) = sum(j=0..m-1) A_j*X^j
R(X) = sum(i) z_i*L_i(X) = sum(j=0..m-1) B_j*X^j
```

Now check their coefficient commitments:

```text
A_j*G + B_j*H = C_j       for j < k
A_j*G + B_j*H = identity  for k <= j < m
```

These are **exactly equivalent** to the original m evaluation equations. To see
why, let V be the Vandermonde matrix with `V[i,j] = x_i^j`. V is invertible over
the scalar field because the coordinates are distinct. The original equations
state that V times the vector of commitment differences is zero. Multiplying
by V's inverse gives zero for each coefficient difference. Conversely, zero
coefficient differences imply every original evaluation equation.

This equivalence uses group linearity over its scalar field, not an additional
security assumption or a random challenge. The native group/scalar primitives
remain those documented by [libsodium](https://libsodium.gitbook.io/doc/advanced/point-arithmetic/ristretto).

### Two-share example

At coordinates 1 and 2:

```text
A0 = 2*y1 - y2       A1 = y2 - y1
B0 = 2*z1 - z2       B1 = z2 - z1
```

Taking twice the first original equation minus the second gives the C0 check.
Subtracting the first equation from the second gives the C1 check. The recovered
secret is already A0. All arithmetic is modulo the Ristretto group order.

### Extra shares still participate

When `m > k`, higher coefficients must **open the identity**. Requiring both
scalars individually to be zero would impose a stronger condition than the
original equations. The implementation checks the group identity opening instead.
A dedicated algebra test uses a test-only generator relation to construct a
nonzero pair opening the identity and verifies that this remains accepted.
Production retains the fixed transparent H and exposes no caller-selected H API.

Checking only A0/C0 would be insufficient. Another test adds X to a value
polynomial: the recovered constant is unchanged, but C1 no longer matches, and
both original and optimized verification reject it.

The optimized path evaluates all coefficient checks in every block and combines
their results without early rejection at a coefficient index. Padding checks
also accumulate before returning. This avoids introducing a first-matching-
coefficient timing signal through the equation transformation. It is not a
universal constant-time claim for every JIT/native runtime.

## Construct the scalar interpolation matrix cheaply

The coefficients of each Lagrange basis polynomial are

```text
L_i(X) = product(j != i)(X - x_j) / d_i
d_i    = product(j != i)(x_i - x_j)
```

For at most nine one-byte coordinates, each numerator has at most eight factors.
The sum of absolute integer coefficients is
`product(j != i)(1 + x_j) < 2^64` at the largest count with distinct coordinates.
Each denominator magnitude is at most `254^8 < 2^64`. Signed `Int128` therefore
constructs all numerator polynomials and denominators exactly, with ample room
for intermediate values. This work depends only on public coordinates.

Signed integers are then encoded into the scalar field. Negative values map to
`l - magnitude`. Independent BigInteger tests check every matrix coefficient,
reversed/sparse coordinates, 256 varied small-coordinate sets, and the near-boundary
set 247–255. They also check the full identity `L_i(x_j) = delta(i,j)`.

Denominator inversion uses Montgomery's batch-inversion trick. Form prefix
products `p_i = d_0*...*d_i`, invert the final product once, then walk backwards:

```text
inverse(d_i) = inverse(p_i) * p_(i-1)
inverse(p_(i-1)) = inverse(p_i) * d_i
```

This needs one inverse and `3*(m-1)` field multiplications. Distinct coordinates
ensure there are no zero denominators. Each numerator coefficient is scaled by
its denominator inverse to form an `m*m` scalar matrix, reused for both private
polynomials and both data blocks.

## A bounded path for small secrets

Coefficient verification is selected when **m is 2–9 and the secret is at most
62 bytes** (two 31-byte blocks). It uses all supplied shares, even when m exceeds
the original threshold. Larger secrets or quorums use the previous evaluation-
verification/interpolation structure, with the same H and coordinate improvements.
Empty secrets retain their framing/digest/coordinate checks and return zero bytes.

For small secrets, the reconstructed bytes fit in at most 62 stack bytes. They
are copied to the caller only after all group equations and byte-padding checks
pass. This avoids the old second interpolation pass for those secrets. Private
coefficient storage is at most 576 bytes; the public basis matrix is at most
2,592 bytes. Matrix construction adds bounded public denominator/prefix/inverse
scratch and a nine-element Int128 polynomial. There is no unbounded stack growth,
heap buffer, pool or cross-call cache. All private coefficients, temporary scalar
products, private group intermediates and plaintext scratch are wiped in finally.

The original canonical point/scalar checks, digest/set binding, quorum/count/range
checks, duplicate rejection, input/output overlap rules and output suffix rules
remain enforced. False results leave the destination unchanged. No invalid extra
share is dropped. Successful warmed calls and measured non-throwing rejection
paths allocate zero managed bytes.

## Expected cost reduction

Original evaluation verification needs `m*(k+1)` full-width group multiplications
per block: two on each left side and k-1 in its Horner evaluation. Coefficient
verification needs `2*m`, plus scalar interpolation. When m=k=3 this changes 12
full-width multiplications to 6; when m=k=9 it changes 90 to 18. The fixed cost of
scalar interpolation is small enough to be useful at the selected sizes, as the
paired benchmarks determine. The separate public-coordinate chains reduce
right-side work for ordinary single-share verification and the fallback path.

Single-share speedup depends on its coordinate: a participant at x=1 avoids all
right-side scalar multiplications; x=255 retains the full native path. A small
share count does not imply small coordinates. Both dense and sparse coordinates
are therefore tested and measured.

## Validation and reproduction

Release and Debug each pass **410 tests**: 228 non-verifying core, 129 VSS and 53
manager integration cases. New coverage includes the complete interpolation basis,
all-byte coordinate multiplication, all small counts, extra shares, canonical
tampering at every supplied position, unchanged-constant corruption, identity
openings, exact creation compatibility, 62/63-byte and 9/10-share boundaries,
larger fallbacks and allocation checks.

The paired benchmark harness freezes the previous Pedersen implementation and its
Ristretto/native wrappers in test/benchmark-only namespaces. Original snapshot
hashes match the [first VSS increment](../benchmarks/results/2026-11-03-vss/README.md).
Both versions receive identical original-format inputs. Setup checks exact
recovery before measurement. The 96 cases cover 8/32-byte secrets with two,
three, five and nine shares, dense/sparse sets, surplus shares, coordinate 1,
fast-path boundaries and a 1 KiB fallback.

```powershell
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug

dotnet run --project benchmarks/Mrbr.Cryptography.Vss.OptimizationBenchmarks -c Release -- --filter '*' --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 120 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/vss-optimized
```

`-- --profile` runs the bounded native diagnostic separately. It does not replace
BenchmarkDotNet or allocation testing. See the [recorded paired results](../benchmarks/results/2026-11-03-vss-optimized/README.md)
for timings, allocations, throughput, uncertainty and source fingerprints. Native
libsodium remains pinned to 1.0.22. The authenticated-common-commitment requirement
and the implementation's independent-audit status are unchanged.
