# SIMD comparison — 3 November 2026

All 27 benchmark cases completed successfully with **0 managed bytes allocated
per operation** and zero measured Gen 0, Gen 1, or Gen 2 collections. This run
compares automatic SIMD dispatch with the [saved scalar baseline](../2026-11-03-scalar/README.md).

The benchmark source, scenarios, .NET 11 RC SDK/runtime, Release settings, and
Intel Core i7-10750H machine match the baseline. Benchmark-control source hashes
were checked. Both runs use one launch, 30 warmup iterations, 15 measured
iterations, a 250 ms target iteration time, and unroll factor 1. The current
High performance power plan was retained. Creation includes fresh platform RNG
calls; all fixture allocation and share selection happen outside measurement.

## 64 KiB, three-of-five

Times are mean microseconds per public API call. Speedup is scalar mean / SIMD
mean. Reconstruction uses either three reordered shares or all five.

| Operation | Scalar (us) | SIMD (us) | Speedup | Managed B/op |
| --- | ---: | ---: | ---: | ---: |
| Create five shares | 6,163.417 | 298.395 | 20.66x | 0 |
| Reconstruct from three | 1,706.923 | 13.546 | 126.01x | 0 |
| Reconstruct from five | 3,039.324 | 24.285 | 125.15x | 0 |

| Operation | Secret MiB/s | Share MiB/s |
| --- | ---: | ---: |
| Create five shares | 209.45 | 1,047.28 |
| Reconstruct from three | 4,614.02 | 13,842.27 |
| Reconstruct from five | 2,573.61 | 12,868.22 |

Secret rate counts original bytes. Share rate counts packed share bytes produced
or consumed, including coordinates. Both columns were cross-checked against raw
nanosecond means in the JSON output.

## Other payload sizes and limits

For a 32-byte three-of-five secret, creation changes from 3.556 us to 0.654 us
(5.44x), and threshold reconstruction from 1.282 us to 0.476 us (2.69x).
At 1 MiB, creation changes from 105.769 ms to 4.555 ms (23.22x), while threshold
reconstruction changes from 27.168 ms to 0.284 ms (95.71x).

The maximum-threshold 32-byte creation case, 255-of-255, changes from 18.832 ms
to 0.252 ms (74.69x). Its coefficient batches use the 128-bit kernel because
each batch contains 16 byte polynomials.

There is no clear improvement for reconstructing tiny secrets from all 255 shares:
three-of-255 using all shares changes from 1.190 ms to 1.229 ms (0.97x), and
255-of-255 using all shares changes from 1.158 ms to 1.208 ms (0.96x). Their
individual mean confidence intervals overlap. Scalar `O(m^2)` interpolation-weight
setup remains dominant; this increment accelerates the payload arithmetic.

## Approach and verification

Two 16-entry tables implement multiplication by a public coordinate or weight.
Secret nibbles select entries through register shuffles. AVX2 processes 32 bytes
at once, with 128-bit handling for 16-byte blocks and scalar handling for tails.
Creation transposes each bounded coefficient batch into planes before parallel
Horner evaluation, preserving the original entropy-to-polynomial mapping.
At most 8 KiB of coefficient scratch is used on the stack, and both scratch
buffers are wiped in the existing exception-safe cleanup path.

Verification includes:

- 144 passing tests in both Debug and Release.
- Exhaustive SIMD multiplication against independent polynomial long division.
- Exact scalar/128-bit/256-bit/automatic equality at vector and batch boundaries,
  unaligned spans, maximum thresholds, zero coefficients, and unchanged entropy use.
- Zero managed allocations for public and explicitly selected internal kernels.
- AVX2-disabled run: 143 passed, one AVX2-only test skipped.
- All-intrinsics-disabled run: 142 passed, two hardware-only tests skipped.
- Optimized x64 JIT disassembly contains both 128-bit and 256-bit `vpshufb`
  instructions. This diagnostic used tiering disabled; timed benchmarks retained
  the normal runtime settings. It is not a universal constant-time guarantee.

The ARM64 128-bit dispatch is implemented but was not executed on this x64 machine.
Public signatures and the packed share format are unchanged.

## Interpreting uncertainty

These are separate runs on one machine. BenchmarkDotNet reported six multimodal
distribution warnings. Current-run 99.9% confidence-interval half-widths reach
about 11.9%; the scalar run reached about 6.1%. Large payload improvements are
substantial, while small differences require additional investigation. The
comparison CSV records both error margins and whether the individual mean
intervals overlap; that flag is not a formal equivalence test.

## Retained evidence

- [Comparison CSV](comparison.csv): all 27 matched scalar/SIMD means, errors, speedups, rates, and allocations.
- [BenchmarkDotNet Markdown](report.md), [CSV](report.csv), and [HTML](report.html).
- [Full JSON](report-full.json): measurements, statistics, memory diagnostics, and runtime metadata.
- [Run log](console.log): build output, warmups, samples, outliers, and warnings.
- [JIT disassembly](jit-disassembly.asm): diagnostic optimized code for the SIMD helpers.
- [Metadata](metadata.json): exact command, machine/runtime details, source fingerprints, and verification results.
