# Interpolation weights for small secrets

Reconstruction still accepts exactly the same packed shares and returns exactly
the same bytes. The change is confined to calculating the public interpolation
weights. Share creation, random coefficients, GF(256) secret arithmetic, SIMD
payload kernels and public API signatures are unchanged.

## Why weights matter

For `m` supplied shares and `L` secret bytes, reconstruction performs two jobs:

1. Compute `m` weights from the share coordinates.
2. Multiply every share payload by its weight and XOR the results.

The first job depends on `m`, not `L`. Its cost is therefore prominent for an
eight-byte handle or a 16/24/32-byte key. Here `m` means the number of supplied
shares, even when the original threshold is smaller. All supplied shares continue
to participate.

The original code computed each numerator and denominator independently, followed
by division. Its inverse operation uses eleven field multiplications, and division
adds one more. The complete weight setup performed `2*m*m + 10*m` field
multiplications: 28 for two shares, 48 for three, 100 for five and 252 for nine.
Each scalar multiplication performs eight reduction steps.

## The same Lagrange formula

Each payload byte is a polynomial evaluation `y_i = p(x_i)` over GF(256). We need
the constant coefficient `p(0)`, not the other coefficients:

```text
w_i  = product(j != i) x_j / product(j != i) (x_i XOR x_j)
p(0) = XOR_i (w_i * y_i)
```

Share coordinates are nonzero and distinct, so every factor and denominator is
nonzero. Introducing the shared numerator `P = product(j) x_j` gives

```text
w_i = P / (x_i * product(j != i)(x_i XOR x_j))
```

These operations use the existing polynomial modulus `0x11B`; the byte model is
described in [NIST FIPS 197, section 4](https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.197-upd1.pdf).
The threshold scheme and its field are unchanged.

## Replace public field products with exact exponent arithmetic

The 255 nonzero field elements form a cyclic multiplicative group. Choose a
generator `g` and write every nonzero element uniquely as `g^e`, where
`0 <= e < 255`. Then

```text
log(a * b) = log(a) + log(b)  (mod 255)
log(a / b) = log(a) - log(b)  (mod 255)
```

These are exact finite-field logarithms, not floating-point logarithms.
Logarithm sums and differences use ordinary integer arithmetic modulo 255;
addition of field elements such as `x_i + x_j` still uses XOR.
[FLINT's finite-field arithmetic documentation](https://flintlib.org/doc/algorithms/finite-fields/zech-logarithms.html)
describes this representation. In this library `g = 0x03` with the existing
`0x11B` modulus. The reproducible generator checks that successive powers visit
all 255 nonzero bytes before returning to one; it does not assume every nonzero
element is a generator.

The general weight calculation becomes

```text
total = sum(j) log(x_j)
d_i   = log(x_i) + sum(j != i) log(x_i XOR x_j)
w_i   = exp((total - d_i) mod 255)
```

The difference `x_i XOR x_j` is symmetric. Read its logarithm once for each
unordered pair and add it to both denominator sums. No finite-field
multiplication or inversion is needed in this helper. The general algorithm
remains `O(m^2)`, with much cheaper work per pair.

The log table has 256 bytes; its zero entry is unused. The exponent table contains
two periods of 255 bytes, allowing the two-share quotient to avoid a remainder
operation. Together they contain **766 bytes of immutable assembly data**.
`ReadOnlySpan<byte>` literals reference that data without constructing managed
arrays. There is no coordinate-set cache, dictionary, mutable shared state or I/O.

Only validated **public coordinates and coordinate-derived values** index these
memory tables. Secret bytes and share payloads continue through `Gf256` and
`Gf256Simd`; their arithmetic has not been replaced with secret-indexed tables.

## Obtain the last weight without another quotient

The Lagrange basis reproduces the constant polynomial `1`. Therefore

```text
w_0 XOR w_1 XOR ... XOR w_(m-1) = 1
```

After computing the first `m-1` weights, obtain the last by XORing them with `1`.
The helper consequently does not need the final denominator sum. This does not
discard the final share: reconstruction still multiplies its payload by that
last weight.

For two shares, the entire calculation is

```text
d   = x_0 XOR x_1
w_0 = x_1 / d
w_1 = 1 XOR w_0
```

Three shares use an unrolled form of the same general formula, with one shared
coordinate difference. These fast paths work with any distinct nonzero
coordinates; they do not assume coordinates `1,2` or `1,2,3`.

For example, use `p(x) = 0x42 + 0x11*x` at coordinates `1` and `2`:

| Coordinate | Payload | Weight |
| --- | --- | --- |
| `01` | `53` | `F7` |
| `02` | `60` | `F6` |

`F7 XOR F6 = 01`. The weighted linear coefficients cancel, leaving
`F7*53 XOR F6*60 = 42`, with multiplication in GF(256). Larger-looking byte values
are normal field elements, not ordinary integer fractions.

## Buffers and validation

`Reconstruct` validates framing, counts, destination size, overlap and every
coordinate before invoking the weight helper. The two/three-share paths add no
scratch buffer. For four or more shares, denominator sums use `m-1` stack integers,
at most 1,016 bytes for the 255-share limit. Together with the existing coordinate,
duplicate-tracking and weight buffers, maximum buffer lengths total 1,782 bytes,
plus ordinary locals, alignment and call frames. Scratch does not depend on
secret length. These buffers contain public metadata, not secret payload copies.

Parameter errors still precede destination writes. Output suffixes remain
untouched. No successful reconstruction allocates managed memory after runtime
initialization. The algorithm neither authenticates shares nor changes how mixed
or modified payloads behave.

## Verification and measurements

The test suite adds:

- All **64,770 ordered pairs** of distinct nonzero coordinates, checked against
  independent polynomial long division and exhaustive inverse search.
- **4,096** varied coordinate sets with two through nine shares.
- Dense and sparse sets, reversed/rotated input order, and counts through 255.
- Interpolation of the monomials `1,x,...,x^(m-1)` at zero for single-digit counts.
- Public reconstruction checked against an independent oracle and the frozen
  SIMD baseline, including arbitrary payloads and more shares than the threshold.
- Payload/vector boundaries, output suffixes, invalid-coordinate rejection and
  zero managed allocations for 8/16/24/32-byte secrets.

Release and Debug each pass 228 core tests and the existing 53 integration tests.
AVX2-disabled and all-intrinsics-disabled runs exercise the unchanged fallback
payload kernels. The table generator is checked with
`python eng/generate-coordinate-tables.py --check`.

The dedicated benchmark project compares the frozen pre-change reconstruction
against the optimized public method, using identical coordinates, payloads,
validation and SIMD kernels. Both dense and sparse sets are measured for every
count from two through nine, alongside vector-width, large-payload and larger-count
checks. A separate group isolates weight calculation from validation and payload
work. See the [recorded results](../benchmarks/results/2026-11-03-weights/README.md)
for timings, throughput, allocations, uncertainty and reproduction commands.
