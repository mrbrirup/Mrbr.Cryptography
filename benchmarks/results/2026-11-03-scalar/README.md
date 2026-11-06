# Scalar baseline — 3 November 2026

All 27 benchmark cases completed successfully. Every case measured **0 managed
bytes allocated per operation** and zero Gen 0, Gen 1, or Gen 2 collections.
Production library source and checked-arithmetic settings were unchanged during
this increment; their SHA-256 fingerprints are recorded in [metadata.json](metadata.json).

## Environment and method

- Intel Core i7-10750H, 6 physical cores / 12 logical processors, Windows 10 22H2 x64.
- SDK `11.0.100-rc.3.26522.148`, runtime `11.0.0-rc.3.26522.148`, RyuJIT,
  concurrent workstation GC, Release build, no attached debugger.
- BenchmarkDotNet `0.16.0-preview.2`; current High performance power plan retained.
- One launch per case, 30 warmup iterations, 15 measured iterations, target
  iteration time 250 ms, unroll factor 1. Actual time can exceed the target for
  expensive calls. BenchmarkDotNet's outlier handling leaves 12–15 samples in
  the reported statistics.
- Buffers, reconstruction share selection, and correctness checks occur in global
  setup. Creation includes the real platform RNG and scratch wiping. Reconstruction
  includes coordinate validation and interpolation-weight calculation on every call.
- Allocation results exclude global setup/cleanup, caller buffer allocation, and
  runtime initialization. This is a warmed, single-threaded public-API baseline.

The complete run took approximately 6 minutes 54 seconds, including build steps.
The [benchmark guide](../../README.md) contains the exact reproducible command,
scenario matrix, and measurement definitions; the command arguments are also saved
in metadata.

## Three-of-five results

Times below are mean microseconds per API call. Reconstruction uses either exactly
three reordered shares or all five shares.

| Secret bytes | Create shares (us) | Reconstruct 3 (us) | Reconstruct 5 (us) |
| ---: | ---: | ---: | ---: |
| 32 | 3.556 | 1.282 | 2.332 |
| 1,024 | 96.019 | 26.956 | 44.878 |
| 65,536 | 6,163.417 | 1,706.923 | 3,039.324 |
| 1,048,576 | 105,769.082 | 27,167.728 | 46,373.616 |

For the 64 KiB secret, the rates and timing uncertainty are:

| Operation | Mean +/- Error (ms) | Secret MiB/s | Share MiB/s | Allocated B/op |
| --- | ---: | ---: | ---: | ---: |
| Create five shares | 6.163 +/- 0.093 | 10.14 | 50.70 | 0 |
| Reconstruct from three | 1.707 +/- 0.040 | 36.62 | 109.85 | 0 |
| Reconstruct from five | 3.039 +/- 0.178 | 20.56 | 102.82 | 0 |

Error is half of the 99.9% confidence interval. Secret rate counts original bytes;
share rate counts packed share bytes produced or consumed, including coordinates.
The rates were independently cross-checked against the full JSON timing data.

## What the other cases show

At 64 KiB, creation takes approximately 1.949 ms for two-of-three, 6.163 ms for
three-of-five, 23.553 ms for five-of-ten, and 104.952 ms for ten-of-twenty. This
matches the expected growth in polynomial work with `n * (k - 1)`.

With a 32-byte secret shared three-of-255, reconstruction from three shares takes
1.271 us, while using all 255 takes 1,190.042 us. Interpolation-weight setup becomes
significant at high share counts. Maximum-threshold creation, 255-of-255 for
32 bytes, takes 18,831.659 us. These cases are included in the full reports.

## Timing limits and retained evidence

An initial six-warmup run exhibited a large timing transition during 1 MiB
creation. This retained run uses 30 warmups for every case and no production code
changes. Timing variation remains: BenchmarkDotNet reported two multimodal
distribution warnings, and relative confidence-interval half-widths reach about
6.1%. Small differences should be investigated with additional launches or the
adaptive benchmark settings before attributing them to an optimization.

Use this as a baseline for this machine and runtime. It does not establish
universal throughput or isolate RNG cost from arithmetic.

- [BenchmarkDotNet Markdown report](report.md): all means, errors, throughput, allocation columns.
- [CSV report](report.csv) and [HTML report](report.html): alternative views of the same results.
- [Full JSON](report-full.json): raw measurements, statistics, memory diagnostics, runtime metadata.
- [Numeric summary](numeric-summary.csv): derived rates and nanosecond timings without display rounding.
- [Run log](console.log): build output, warmups, samples, diagnostics, and warnings.
- [Metadata](metadata.json): exact command, environment, source fingerprints, and cross-check results.
