# ShamirSharedSecret: API, mathematics and verification

This guide preserves the detailed raw-sharing API, field mathematics, optimisation
explanations and recorded verification previously held in the repository README.
See the [repository overview](../README.md) for project selection, build commands,
console examples, licensing and optional integrations.

## Public API and share layout

```csharp
int GetShareLength(int secretLength);
int GetSharesLength(int secretLength, int shareCount);
int CreateShares(ReadOnlySpan<byte> secret, int threshold, int shareCount, Span<byte> destination);
int Reconstruct(ReadOnlySpan<byte> shares, int shareLength, int threshold, Span<byte> destination);
```

Use `2 <= threshold <= shareCount <= 255`. Any byte sequence is accepted, including
an empty one. Sizes must fit in a single span; sizing helpers reject integer
overflow. Input and destination spans must not overlap, although disjoint slices
of the same underlying allocation are supported.

For a secret of length `L`, each share is `L + 1` bytes:

| Offset | Contents |
| --- | --- |
| 0 | Coordinate `x`, from 1 through 255 |
| 1 through L | Polynomial evaluation for each secret byte |

`CreateShares` writes consecutive shares with coordinates `1, 2, ..., shareCount`.
`Reconstruct` takes complete, equally sized shares concatenated in any order; the
count is inferred from the buffer length. All supplied shares participate. At least
`threshold` shares are required, with distinct, nonzero coordinates. Both methods
return the number of bytes written and leave any destination suffix untouched.
Pass only populated share bytes, excluding spare capacity, to reconstruction.

This is a raw in-memory layout. The threshold and individual share length must be
retained by the caller. There is no version header, group identifier, checksum, or
authentication tag. Wrong thresholds, mixed share sets, and structurally valid
modified payloads can produce incorrect output without an error. These APIs do
not authenticate data, correct errors, encode text, or perform file/network/console I/O.

### Example: create five shares, recover from coordinates 2, 4, and 5

```csharp
using System.Security.Cryptography;
using Mrbr.Cryptography;

ReadOnlySpan<byte> secret = [0x42, 0x00, 0xFF];
const int threshold = 3;
const int shareCount = 5;
int shareLength = ShamirSharedSecret.GetShareLength(secret.Length);
Span<byte> shares = stackalloc byte[ShamirSharedSecret.GetSharesLength(secret.Length, shareCount)];

ShamirSharedSecret.CreateShares(secret, threshold, shareCount, shares);

Span<byte> selected = stackalloc byte[threshold * shareLength];
shares.Slice(shareLength, shareLength).CopyTo(selected);
shares.Slice(3 * shareLength, shareLength).CopyTo(selected[shareLength..]);
shares.Slice(4 * shareLength, shareLength).CopyTo(selected[(2 * shareLength)..]);

Span<byte> recovered = stackalloc byte[secret.Length];
int written = ShamirSharedSecret.Reconstruct(selected, shareLength, threshold, recovered);
if (!secret.SequenceEqual(recovered[..written]))
{
    throw new InvalidOperationException("Reconstruction failed.");
}

CryptographicOperations.ZeroMemory(recovered);
CryptographicOperations.ZeroMemory(selected);
CryptographicOperations.ZeroMemory(shares);
```

The stack allocations in this example are deliberately small. For large data, the
caller should allocate or reuse appropriately sized buffers rather than put large
payloads on the stack. The library's scratch sizes do not grow with secret length.
The caller owns and clears its buffers when finished, including the original secret
when it is stored in writable memory.

## The mathematics

### 1. A byte represents a polynomial

Bit `i` is the coefficient of `t^i`, with coefficients in GF(2), the field `{0, 1}`.
For example, `0x57 = 01010111` represents `t^6 + t^4 + t^2 + t + 1`.
Eight bits give `2^8 = 256` possible elements.

### 2. Addition and subtraction are XOR

Coefficients add modulo two, without carries: `1 + 1 = 0`.
Thus `0x57 + 0x83 = 0xD4`. Every element is its own additive inverse,
so subtraction also uses XOR and `a + a = 0`.

### 3. Multiplication includes polynomial reduction

Multiply the polynomials, then take the remainder modulo the irreducible polynomial
`m(t) = t^8 + t^4 + t^3 + t + 1`, encoded as `0x11B`.

For example, `0x80 * 0x02` represents `t^8`. Its remainder is
`t^4 + t^3 + t + 1 = 0x1B`. This keeps results within eight bits.
Another published example is `0x57 * 0x13 = 0xFE`.

This is polynomial arithmetic, not integer arithmetic modulo 256. The polynomial
and examples above follow [NIST FIPS 197, section 4](https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.197-upd1.pdf).
Using this field arithmetic does not mean this library implements AES.

### 4. Division uses an inverse

The 255 nonzero elements form a multiplicative group. Consequently,
`a^255 = 1`, so `a^-1 = a^254` and `a / b = a * b^-1` for nonzero `b`.
Zero has no inverse; inversion of zero and division by zero throw
`DivideByZeroException`, including `0 / 0`.

`Inverse` builds powers along the fixed chain
`2, 3, 6, 12, 15, 30, 60, 120, 240, 252, 254`.
Each step multiplies powers already computed, for eleven field multiplications
rather than 253 repeated multiplications.

### 5. How this supports Shamir sharing

There are two polynomial layers: a byte is represented internally as a polynomial
in `t`; the sharing polynomial `f(X)` has those field elements as its coefficients.
For a threshold `k`, each secret byte `s` receives its own polynomial:

```text
f(X) = s + a1*X + a2*X^2 + ... + a(k-1)*X^(k-1)
```

Each share stores `f(x)` at its coordinate. The constant term is the secret because
`f(0) = s`; coordinate zero must never be issued. Coefficients are fresh, independent,
uniform random bytes for every secret byte and every creation call. Zero coefficients
are allowed, including the highest coefficient: the degree is at most `k - 1`.
Resampling to force a nonzero highest coefficient changes the required distribution.
The threshold construction follows [Shamir's original paper](https://people.cs.pitt.edu/~litman/courses/cs2001/lectures/lecture-02-p612-shamir.pdf).

### 6. Evaluate with Horner's rule

For `k = 3`, rewrite `s + a1*x + a2*x^2` as `(a2*x + a1)*x + s`.
The implementation starts with the highest coefficient, repeatedly multiplies by
the coordinate and adds the next coefficient, and finally incorporates the secret.
This takes `k - 1` field multiplications without constructing a powers array.

For a concrete example, choose `s = 0x42`, `a1 = 0x11`, and `a2 = 0x7E`.
At coordinate `0x02`, Horner evaluation is:

```text
0x7E * 0x02 = 0xFC
0xFC + 0x11 = 0xED
0xED * 0x02 = 0xC1
0xC1 + 0x42 = 0x83
```

Every operation uses GF(256). The test suite fixes this polynomial's values, along
with two additional byte polynomials, independently of the production evaluator.
Production creation uses the platform cryptographic RNG; chosen coefficients are
available only through the internal test seam.

### 7. Reconstruct with Lagrange interpolation at zero

For `m >= k` genuine shares `(xi, yi)`, interpolation recovers `f(0)` directly:

```text
lambda_i = product(j != i, xj) / product(j != i, xi + xj)
secret   = sum(i, yi * lambda_i)
```

These are Lagrange basis weights evaluated at zero. Addition and subtraction are
the same here, so the usual negative signs become field addition. Distinct
coordinates ensure no denominator is zero. We need only `f(0)`, not the full list
of polynomial coefficients.

Using coordinates 2, 4, and 5 from the polynomial above gives this worked example.
All entries are hexadecimal field elements:

| Coordinate | Payload | Weight at zero | Weighted payload |
| --- | --- | --- | --- |
| 02 | 83 | D0 | 64 |
| 04 | A7 | 03 | F2 |
| 05 | C8 | D2 | D4 |

The recovered byte is `0x64 XOR 0xF2 XOR 0xD4 = 0x42`.
The implementation computes weights once and reuses them for every byte. The
coordinate-only weight helper evaluates the same products and quotients using
exact logarithm/exponent tables and shared pairwise coordinate differences.
The resulting work is `O(m^2 + L*m)` for `L` secret bytes, rather than recomputing
weights separately for each byte. Creation takes `O(L * shareCount * (k - 1))`.

### 8. Process many bytes with SIMD

Multiplication by a fixed field element is linear. Split a byte into a low nibble
and a high nibble, so `b = low XOR (high << 4)`. For a public multiplier `c`:

```text
c * b = LowTable[low] XOR HighTable[high]
LowTable[n]  = c * n
HighTable[n] = c * (n << 4)
```

The two tables each have 16 entries. `Gf256Lookup` constructs them from the basis
products `c, c*x, ..., c*x^7`. The multiplier is a public share coordinate during
creation or a public-coordinate-derived interpolation weight during reconstruction.
Secret nibbles select entries using register shuffles, not memory indexing.

The AVX2 path handles 32 payload bytes at once. Each 128-bit half receives a copy
of the same table because [AVX2 byte shuffles](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.x86.avx2.shuffle?view=net-10.0)
operate within those halves. The 128-bit path handles 16 bytes with
[`Vector128.Shuffle`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.vector128.shuffle?view=net-10.0),
enabled for SSSE3 on x86/x64 or AdvSimd on ARM64. Feature checks select available
instructions; scalar code handles unsupported hardware and partial-vector tails.
Loads and stores are restricted to complete vectors inside the supplied spans.

For creation, coefficients arrive grouped by secret byte, as before. A bounded
transpose groups them into planes: all `a1` values, then all `a2` values, etc.
Vector lanes can then evaluate independent byte polynomials with the same Horner
steps. The transpose preserves exactly which entropy bytes belong to each
polynomial. Degree-one coefficients already form a plane, so no transpose is needed.

Reconstruction computes weights with the coordinate-only logarithm helper, then
multiplies each payload vector by its share's weight and XORs the result into the
destination. The SIMD payload kernels are unchanged. Weight setup remains
`O(m^2)`, using table reads and integer additions instead of repeated field
multiplication and inversion. See the [weight guide](interpolation-weights.md).

## Implementation and verification

Creation fills a 4096-byte stack buffer in batches using
[`RandomNumberGenerator.Fill`](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.fill?view=net-11.0).
Each batch contains complete coefficient sets for its secret bytes; exactly
`L * (k - 1)` random bytes are consumed overall. The coefficient buffer is cleared
in a `finally` block using
[`CryptographicOperations.ZeroMemory`](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-11.0).
If randomness fails, the entire output prefix is cleared and the exception is
propagated. Parameter errors occur before output writes or randomness requests.
Empty secrets create coordinate-only shares without requesting entropy.

When SIMD creation needs coefficient planes, it uses a second stack buffer of
at most 4096 bytes. Both buffers are wiped in the same `finally` block. Maximum
coefficient scratch storage is therefore 8192 bytes, independent of secret length.
Scalar creation and degree-one creation do not need the extra buffer.

Reconstruction uses at most 766 bytes of buffers for public coordinates, duplicate
tracking and weights. For four or more shares, it adds at most 1,016 stack bytes
for public denominator sums, totaling at most 1,782 buffer bytes plus locals and
alignment. Two/three-share fast paths need no additional scratch buffer. It has no RNG dependency.
Successful calls create no arrays, pooled buffers, delegates, or boxed values.
Allocations from initialization, caller-owned buffers, and exceptions are outside
the zero-allocation contract.

Scalar field multiplication runs eight iterations using bit masks, with no
operand-dependent tables or branches. Inversion uses a fixed chain after rejecting
zero. Loops and indexing in sharing depend on public lengths, thresholds, and
coordinates. Generated machine code depends on the runtime and CPU; this is not
a universal constant-time guarantee. SIMD table construction uses public values;
payload-dependent nibble selection stays in vector registers. The weight
calculations additionally use ordinary memory-table reads indexed only by
public coordinates. Secret bytes never index those memory tables. Optimized x64 JIT
disassembly was checked for 128-bit and 256-bit shuffle instructions. The ARM64
dispatch is implemented but has not been executed on this x64 development machine.
The [benchmark suite](../benchmarks/README.md) compares the current implementation
against the retained scalar baseline.

The tests cover:

- Published NIST examples and field identities.
- All 65,536 multiplication input pairs against an independent reference that
  forms the full polynomial product and then performs polynomial long division.
- Distributivity for every byte pair and each of the eight polynomial basis elements.
- Every nonzero inverse against a brute-force search using that reference.
- All 65,280 valid division input pairs and rejection of every zero-denominator case.
- Zero managed allocations across all five operations after warmup, measured on
  the current thread with assertions outside the measurement interval.
- Fixed creation vectors and independent direct-power polynomial evaluation.
- Every three-of-five subset in all six orders, extra shares, arbitrary binary
  data, empty secrets, and the 255-share/255-threshold boundary.
- Fresh coefficient consumption across RNG batch boundaries, including zeros;
  simulated RNG failures before and after partially producing shares.
- Rejection of insufficient shares, bad framing, invalid or duplicate coordinates,
  invalid parameters, short buffers, and overlapping spans without output mutation.
- Disjoint slices of shared backing memory, unchanged inputs, and untouched suffixes.
- Zero managed allocations in both public APIs using the real platform RNG,
  including multiple batches and maximum-threshold operation.
- A small secrecy demonstration: an observed point in a two-share scheme is
  compatible with every possible secret byte when its unknown coefficient varies.
- Both SIMD widths against an independent reference for every byte-pair product,
  including the two AVX2 shuffle halves.
- Exact scalar/128-bit/256-bit/automatic creation and reconstruction parity at
  vector tails, unaligned span offsets, coefficient-batch boundaries, and maximum thresholds.
- Zero coefficients, unchanged entropy consumption, untouched guards, and zero
  managed allocations for SIMD and scalar paths.
- Separate runs with AVX2 disabled and with all hardware intrinsics disabled,
  exercising automatic 128-bit and scalar fallback respectively.

The weight tests additionally cover all 64,770 ordered nonzero coordinate pairs,
4,096 varied single-digit coordinate sets, dense/sparse sets through 255 shares,
monomial interpolation identities, coordinate permutations, exact equivalence to
the frozen SIMD baseline, and zero allocations. The full integration solution now
passes 522 tests in Release and Debug: 228 core, 129 VSS, 50 authentication,
53 existing manager integration and 62 authenticated key-recovery tests.
The standalone solution runs 407 tests.

The independent reference, brute-force inverse search, and deterministic entropy
sources live only in the test project. Allocation checks exclude initialization
and exception paths; they are regression checks, not throughput benchmarks.
