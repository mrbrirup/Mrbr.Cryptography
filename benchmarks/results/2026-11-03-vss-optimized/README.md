# Optimized Pedersen VSS: paired verification and reconstruction

All **96 cases / 48 matched comparisons** completed successfully. Every original
and optimized case reported **0 managed B/op and zero measured GC collections**.
The artifacts are dated by the run's 2026-11-03 start; exact UTC capture/completion
timestamps are in metadata.

For the measured 8/32-byte secrets with matching two-, three-, five- and nine-share
quorums, verified reconstruction improved **1.73x–7.06x**. Single-share verification
depends strongly on its public coordinate: coordinate 1 improved up to **3.00x**,
while several native-multiply/sparse cases had overlapping confidence intervals
and no resolved improvement.

The public APIs, wire version, point/scalar encodings, generator H, threshold
semantics, authenticated-common-commitment requirement and randomness distribution
are unchanged. All non-verifying core source/project fingerprints remain identical
to the preceding increment. Its code and dependencies were not modified.

## Reconstruction of 32-byte secrets

Means in **milliseconds**. k is the committed threshold; m is the number of
supplied shares. Every supplied share participates and must satisfy verification.

| k | m | Coordinates | Original ms | Optimized ms | Speedup |
| ---: | ---: | --- | ---: | ---: | ---: |
| 2 | 2 | Dense | 1.204 | 0.679 | 1.77x |
| 2 | 2 | Sparse | 1.177 | 0.680 | 1.73x |
| 3 | 3 | Dense | 2.441 | 0.991 | 2.46x |
| 3 | 3 | Sparse | 2.511 | 1.019 | 2.46x |
| 5 | 5 | Dense | 6.640 | 1.698 | 3.91x |
| 5 | 5 | Sparse | 6.428 | 1.633 | 3.94x |
| 9 | 9 | Dense | 20.317 | 2.982 | 6.81x |
| 9 | 9 | Sparse | 21.020 | 2.976 | 7.06x |
| 3 | 5 | Dense, extra shares | 4.115 | 1.622 | 2.54x |
| 3 | 9 | Sparse, extra shares | 7.321 | 2.919 | 2.51x |

Eight-byte matching-quorum reconstruction improved **1.78x–6.80x** in the measured
sets. Its nine-share dense case changed from 10.448 ms to 1.537 ms; the sparse
case changed from 10.177 ms to 1.687 ms. Individual samples and error margins for
every pair are retained in the CSV/JSON, rather than inferred from rounded tables.

The main change is exact coefficient verification. Interpolate both private
polynomials, then verify each coefficient commitment, padded with identity
commitments beyond degree k-1. An invertible Vandermonde change of basis makes
this equivalent to the original per-share equations. It reduces full-width
group multiplication counts per block from `m*(k+1)` to `2*m`, adding inexpensive
scalar interpolation. It does not combine equations into one probabilistic check.

This path is selected for **2–9 supplied shares and at most 62 secret bytes**.
Public Lagrange basis coefficients are constructed with exact bounded Int128
arithmetic, and denominator inverses use one batch inversion. The at-most-62-byte
plaintext fits stack scratch and is copied only after all checks, avoiding the
old second interpolation pass. Private scratch is wiped in finally.

## Individual verification

Means in milliseconds for 32-byte secrets. Dense targets its largest coordinate
unless explicitly marked x=1. Sparse targets coordinate 255.

| k | Coordinate | Original ms | Optimized ms | Speedup | Mean CIs overlap? |
| ---: | --- | ---: | ---: | ---: | --- |
| 2 | 2 | 0.584 | 0.419 | 1.39x | No |
| 3 | 3 | 0.837 | 0.609 | 1.37x | No |
| 3 | 1 | 0.848 | 0.441 | 1.92x | No |
| 5 | 5 | 1.376 | 1.208 | 1.14x | No |
| 9 | 9 | 2.347 | 2.329 | 1.01x | Yes |
| 9 | 1 | 2.414 | 0.804 | 3.00x | No |
| 3 | 255 | 0.826 | 0.812 | 1.02x | Yes |
| 9 | 255 | 2.481 | 2.361 | 1.05x | Yes |

The fixed, transparently derived H is now the same 32-byte immutable assembly-data
encoding, checked against independent SHA-512/hash-to-group derivation in tests.
This removes its repeated derivation without a heap cache or generator change.

On this host, a bounded native diagnostic measured encoded addition at about
23.64 us, H derivation at 25 us and full point multiplication at 92–101 us. The
public-coordinate helper therefore uses at most three encoded additions for
coordinates **1–6 and 8**, with full native multiplication for the rest. Private
scalars never use this variable-time coordinate helper. Coordinate 1 simply
copies the point, removing the right-side scalar multiplications in point Horner.

These results do not support a universal individual-verification speedup. For
example, the eight-byte, nine-share sparse verification case measured 1.212 ms
original versus 1.303 ms optimized, but its mean confidence intervals overlap.
That case retains full native coordinate multiplication, and small timing
differences are not resolved reliably by this run. The overlap flag is not a
formal statistical equivalence test.

## Fast-path boundaries and fallback

| Scenario | Operation | Original ms | Optimized ms | Speedup |
| --- | --- | ---: | ---: | ---: |
| 62 B, k=m=3, dense | Reconstruction, optimized coefficient path | 2.597 | 1.015 | 2.56x |
| 63 B, k=m=3, dense | Reconstruction, fallback | 3.640 | 2.420 | 1.50x |
| 32 B, k=m=10, sparse | Reconstruction, fallback | 24.385 | 22.850 | 1.07x, CIs overlap |
| 1 KiB, k=m=3, dense | Single verification | 13.916 | 10.557 | 1.32x |
| 1 KiB, k=m=3, dense | Reconstruction, fallback | 39.547 | 25.128 | 1.57x |

Larger cases retain evaluation verification and the original interpolation/output
structure. They still benefit where their public coordinates take a short chain,
and from immutable H. The count-10 sparse timing difference is unresolved within
its overlapping confidence intervals.

## Exactness, inputs and baseline

The baseline freezes the complete previous `PedersenVss`, `Ristretto` and `Sodium`
sources. Original `.cs.original.txt` snapshots match the hashes in the
[first VSS report](../2026-11-03-vss/README.md). Compiled baseline copies change
namespaces and type references only, and use the same libsodium 1.0.22 binary.
Baseline code is compiled only into test/benchmark assemblies. It is not in the
production library.

For each scenario, the original creator generates the fixed reconstruction inputs.
Both implementations receive identical data, commitments and coordinate order;
setup verifies correct recovery before measurement. Dense sets are m,m-1,...,1;
the x=1 variants reverse that order. Sparse sets start 255,73,146,219,37,... using
`((254 + 73*i) mod 255) + 1`, with a packet declaring 255 total shares. Dense packets
declare m total shares. Thus k/m labels describe the supplied quorum and are not
claims that every case is a k-of-five distribution.

The optimized coefficient checks all run, across all small-secret blocks, before
returning verification failure. Surplus coefficients must open the group identity;
they are not required to contain two individually zero scalars. Tests demonstrate
that distinction with a test-only known-generator relation. Production keeps its
fixed transparent H. No invalid extra share is omitted. No random batching or
cross-call private state is introduced.

## Validation, memory and uncertainty

Release and Debug each pass **410 tests**, with no build warnings or errors:
228 existing non-verifying core, 129 VSS and 53 manager integration cases.
New tests cover independent BigInteger basis coefficients, `L_i(x_j)=delta(i,j)`,
256 varied single-digit coordinate sets, near-limit integer magnitudes, every byte
coordinate and four public points, dense/sparse and reversed sets, extra shares,
canonical tampering at every supplied position, unchanged-constant corruption,
identity openings, byte-identical deterministic creation, fast-path boundaries,
larger fallbacks and allocation checks. Original malformed-input, cleanup,
padding-failure, RFC vector and protocol tests continue to pass.

All 96 timed cases reported **0 managed B/op and zero GC collections**. Setup,
caller storage, JIT/native/RNG initialization and exception allocations are
excluded. MemoryDiagnoser does not measure native allocator traffic. Native and
source fingerprints remained unchanged during measurement. There were **no
multimodal-distribution warnings**. BenchmarkDotNet reported outliers, removing
some according to its standard policy. Full confidence intervals and samples are
preserved; results are specific to this runtime/host and warmed workload.

Host: Windows 10 22H2, Intel Core i7-10750H, six cores / twelve logical processors,
.NET 11 RC3 x64, SDK `11.0.100-rc.3.26522.148`, native libsodium 1.0.22.
BenchmarkDotNet `0.16.0-preview.2` used one launch, 15 warmups, 12 measured
iterations of 120 ms and unroll factor 1. The existing High performance power
plan was retained. Total time: **477.37 seconds**. Other native platforms were
not executed here; protocol-audit status is unchanged.

## Reproduce

```powershell
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug

dotnet run --project benchmarks/Mrbr.Cryptography.Vss.OptimizationBenchmarks -c Release -- --filter '*' --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 120 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/vss-optimized
```

Run with `-- --profile` for the separate bounded native diagnostic. Generated
benchmark builds may need NuGet even when the outer invocation uses `--no-build`.

- [All 48 paired timings, throughput, error margins, allocations and CI overlap](comparison.csv)
- [Host, source/snapshot/native fingerprints, diagnostic and verification metadata](metadata.json)
- [BenchmarkDotNet Markdown report](artifacts/results/Mrbr.Cryptography.Vss.OptimizationBenchmarks.VerificationBenchmarks-report-github.md)
- [Full JSON/CSV/HTML and execution log](artifacts/)
- [Mathematics, equivalence proof and implementation details](../../../docs/vss-verification-optimization.md)

Historical reports retain their original data and fingerprints.
