# Interpolation-weight optimization: paired SIMD comparison

All **96 benchmark cases / 48 matched pairs** completed on 2026-11-03.
Both versions reported **0 managed B/op and zero GC collections in every case**.
Reconstruction of 32-byte secrets with two through nine supplied shares improved
**6.30x–13.85x**; eight-byte secrets improved **2.44x–4.04x** across the measured
dense and sparse coordinate sets. The separate weight helper improved
**27.26x–47.27x** in the isolated setup measurements.

These measurements compare the optimized public `Reconstruct` method against the
frozen pre-change reconstruction implementation with its existing SIMD payload
kernels. Share creation, scalar secret arithmetic and SIMD kernels were not
changed. Public signatures, validation, packed share format, use of all supplied
shares and output semantics remain the same.

## Small-secret reconstruction

Representative means are in **nanoseconds**, including input validation, weight
calculation and payload reconstruction. All supplied shares participate. The full
CSV contains every count from two through nine, with both coordinate patterns.

| Secret bytes | Shares | Coordinates | SIMD baseline ns | Optimized ns | Speedup |
| --- | ---: | --- | ---: | ---: | ---: |
| 8 | 2 | Dense | 403.39 | 164.42 | 2.45x |
| 8 | 2 | Sparse | 419.14 | 171.51 | 2.44x |
| 8 | 3 | Dense | 700.45 | 272.70 | 2.57x |
| 8 | 3 | Sparse | 692.31 | 258.11 | 2.68x |
| 8 | 5 | Dense | 1321.55 | 447.19 | 2.96x |
| 8 | 5 | Sparse | 1303.26 | 452.59 | 2.88x |
| 8 | 9 | Dense | 3110.45 | 798.67 | 3.89x |
| 8 | 9 | Sparse | 3223.78 | 798.03 | 4.04x |
| 32 | 2 | Dense | 269.33 | 40.91 | 6.58x |
| 32 | 2 | Sparse | 276.86 | 43.92 | 6.30x |
| 32 | 3 | Dense | 459.70 | 60.85 | 7.56x |
| 32 | 3 | Sparse | 462.73 | 57.62 | 8.03x |
| 32 | 5 | Dense | 1046.36 | 102.64 | 10.19x |
| 32 | 5 | Sparse | 934.66 | 101.65 | 9.20x |
| 32 | 9 | Dense | 2600.94 | 187.80 | 13.85x |
| 32 | 9 | Sparse | 2453.66 | 178.94 | 13.71x |

The three-share 16-byte cases improved 6.96x–7.75x; the 24-byte cases improved
2.49x–2.54x. The current SIMD kernels process complete 16/32-byte blocks and use
scalar arithmetic for eight-byte payloads and eight-byte remainders. Once weight
setup is cheaper, that unchanged scalar payload work accounts for a larger
proportion of small-secret reconstruction. Smaller payloads consequently do not
always have lower absolute latency.

## Weight setup alone

Means in nanoseconds. This group excludes reconstruction validation and payload
processing; both methods receive prevalidated coordinates and preallocated output.
The original loop uses the unchanged field multiplication/division helpers. The
optimized helper uses coordinate-only logarithm tables and integer arithmetic.

| Shares | Coordinates | Original ns | Optimized ns | Speedup |
| --- | --- | ---: | ---: | ---: |
| 2 | Dense | 262.09 | 5.89 | 44.46x |
| 2 | Sparse | 254.18 | 7.24 | 35.11x |
| 3 | Dense | 425.18 | 9.27 | 45.89x |
| 3 | Sparse | 438.99 | 9.29 | 47.27x |
| 5 | Dense | 871.18 | 31.96 | 27.26x |
| 5 | Sparse | 869.37 | 29.16 | 29.82x |
| 9 | Dense | 2248.52 | 73.30 | 30.67x |
| 9 | Sparse | 2214.22 | 68.82 | 32.17x |

The weight helper has specialized two/three-share formulas. For other counts it
shares each pairwise difference and accumulates denominator products as sums of
logarithms. The weights XOR to one, allowing the final weight to be obtained from
the others. Its immutable tables occupy 766 bytes of assembly data and do not
construct managed arrays or cache coordinate sets. Lookup indices depend only on
public coordinates. See the [mathematics guide](../../../docs/interpolation-weights.md)
for the derivation and a worked example.

## Regression checks outside the target sizes

| Secret / supplied shares | SIMD baseline | Optimized | Interpretation |
| --- | ---: | ---: | --- |
| 32 B / 32 sparse shares | 20.947 us | 1.088 us | 19.25x |
| 32 B / all 255 coordinates, ascending | 1.109 ms | 43.529 us | 25.48x |
| 32 B / all 255 coordinates, permuted | 1.101 ms | 43.808 us | 25.12x |
| 64 KiB / 3 sparse shares | 12.089 us | 12.185 us | No resolved timing difference |

The 64 KiB mean confidence-interval half-widths are 0.121 us for the baseline
and 0.981 us for the optimized version; their intervals overlap. The small weight
change is dwarfed by payload work and measurement variation in that case.
The 255-coordinate cases exercise the general algorithm; there is no dedicated
all-255 or dense-coordinate shortcut. With all 255 coordinates, the "sparse"
permutation necessarily contains the same full coordinate set in a different order.

## Inputs and baseline fidelity

Dense coordinates are `1,2,...,m`. Sparse coordinates follow
`((254 + 73*i) mod 255) + 1`: they begin `255,73,146,219,37,...`, cover widely
separated byte values for small counts and include the maximum coordinate. Since
73 is coprime to 255, the full permutation has no duplicate or zero coordinates.
Both versions receive identical deterministic payloads and coordinates, created
outside measurement. Setup validates exact baseline/optimized output equality.
Independent tests additionally establish reconstruction correctness.

The source snapshot
`Baselines/ShamirSharedSecret.Simd.cs.txt` has SHA-256
`6E77CC1A2BA7D5D23213E5FE40DB1BC6FA6B61B96375FD5209CE1B0A5EF91CDC`,
matching the core source recorded in the earlier [SIMD baseline](../2026-11-03-simd/README.md).
The compiled frozen reconstruction body was checked verbatim against that
snapshot. It retains the original public wrapper, input validation, weight loop,
payload loop and dispatch. Both measured implementations share the unchanged
`Gf256` and `Gf256Simd` helpers. The frozen reference is compiled only into tests
and benchmarks, not into the production library.

This is a paired comparison on the current host, not a ratio against timings from
an earlier session. There were no multimodal-distribution warnings. BenchmarkDotNet
reported outliers and removed some according to its normal policy; raw samples,
99.9% confidence intervals and logs are retained. Means and speedups remain
specific to these warmed workloads and this runtime/CPU. Very short helper timings
should not be interpreted as a universal per-call latency guarantee.

## Environment, tests and reproduction

- Windows 10 22H2, Intel Core i7-10750H, 6 cores / 12 logical processors.
- .NET 11 RC3 x64, SDK `11.0.100-rc.3.26522.148`, AVX2 available.
- BenchmarkDotNet `0.16.0-preview.2`, Release, no debugger, checked arithmetic.
- Existing High performance power plan retained; no power-plan changes.
- One launch, 15 warmups, 12 measured iterations of 120 ms, unroll factor 1.
- Total benchmark time: 486.8 seconds.
- Release and Debug: **281 passed**, including 228 core and 53 integration cases.
- AVX2 disabled: 227 passed, one hardware-only test skipped.
- All intrinsics disabled: 226 passed, two hardware-only tests skipped.
- No build warnings or errors. Table regeneration check passed.

The new tests include all 64,770 ordered nonzero coordinate pairs, 4,096 varied
single-digit coordinate sets, dense/sparse counts through 255, input permutations,
monomial identities, arbitrary payloads, more supplied shares than the threshold,
buffer suffixes, invalid-coordinate validation and warmed allocation checks.
The unchanged KeyManager/EncryptionManager integration passes alongside them.
ARM64 was not executed on this x64 host.

From the repository root:

```powershell
dotnet build Mrbr.Cryptography.slnx -c Release
dotnet test --solution Mrbr.Cryptography.slnx -c Release --no-build
python eng/generate-coordinate-tables.py --check

dotnet run --project benchmarks/Mrbr.Cryptography.WeightBenchmarks -c Release --no-build -- --filter '*' --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 120 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/weights
```

Use `./eng/Invoke-Integration.ps1 -Action Test -Configuration Release` (or Debug)
to include the checked-out manager projects. BenchmarkDotNet generates a separate
build and can require NuGet access even when the outer run uses `--no-build`.

Retained evidence:

- [All 48 comparisons, including throughput, errors, allocations and interval overlap](comparison.csv)
- [Metadata, verification and source/report fingerprints](metadata.json)
- [Reconstruction report](artifacts/results/Mrbr.Cryptography.WeightBenchmarks.ReconstructionBenchmarks-report-github.md)
- [Weights-only report](artifacts/results/Mrbr.Cryptography.WeightBenchmarks.WeightsOnlyBenchmarks-report-github.md)
- [Full JSON/CSV/HTML reports and execution log](artifacts/)

Source hashes were unchanged throughout measurement. Historical scalar, SIMD and
integration reports retain their original data and source fingerprints.
