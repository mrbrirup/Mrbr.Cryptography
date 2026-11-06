# Ed25519 commitment authentication measurements — 4 November 2026

The optional `Mrbr.Cryptography.Vss.Authentication` layer signs existing Pedersen
commitment packets with public context and generation metadata. This benchmark
compares the existing optimized Pedersen implementation with authenticated calls
using identical shares and commitments. The baseline is current PedersenVss,
not the original implementation frozen before its verification optimizations.
No existing core or VSS source/project file changed; 12 fingerprints are checked
against the preceding increment in [metadata.json](metadata.json).

## Validation and environment

Debug and Release each pass **460 tests**: 228 core, 129 VSS, 50 authentication
and 53 integration. Builds have zero warnings/errors. Authentication coverage
includes three RFC 8032 vectors, a separate test-only BigInteger signer, exact
transcript comparisons, every byte mutation/truncation of a representative
packet, metadata/signature/set substitution, VSS rejection, overlap/output
contracts and warmed managed allocations. See the [protocol and math guide](../../../docs/authenticated-commitment-packets.md).

- Windows 10 22H2 x64; Intel Core i7-10750H, 6 cores/12 logical processors.
- Pinned SDK `11.0.100-rc.3.26522.148`, .NET 11 RC3, x64 RyuJIT, workstation GC.
- libsodium NuGet 1.0.22; exact Windows x64 native binary/package hashes recorded.
- BenchmarkDotNet `0.16.0-preview.2`, Release, no debugger.
- Existing High performance power plan retained; the harness does not change it.
- Caller buffers, key provisioning, VSS share creation and initialization are
  outside measurement. `CreateEnvelope` includes seed-to-key derivation, copying,
  signature generation and temporary secret-key wiping on every call.

## Primary run

One launch, 15 warmups, 12 measurement iterations, 120 ms target iteration,
unroll factor one. All **48 cases measured 0 managed B/op and zero GC
collections**. The full run completed in 253.61 seconds.

Context is the 30-byte ASCII string `Mrbr.KeyRecovery/benchmark-key`; generation
is 42. All scenarios reconstruct exactly k supplied shares at threshold k.
Dense packets issue n=max(5,k) shares; selected coordinates are k down to one.
Sparse packets issue 255 shares; selected coordinates begin 255, 73, 146.
Single-share verification uses the first selected coordinate in both paths.

Times below are means, in microseconds. Error is half the BenchmarkDotNet 99.9%
confidence interval. There is one envelope operation per call, independent of
the supplied reconstruction quorum.

| Scenario | Create envelope, µs | Authenticate packet, µs |
| --- | ---: | ---: |
| 8 B, k=m=3, dense | 74.03 ± 4.61 | 101.50 ± 3.97 |
| 32 B, k=m=2, dense | 73.27 ± 3.22 | 96.21 ± 2.39 |
| 32 B, k=m=3, dense | 72.33 ± 1.54 | 96.89 ± 3.23 |
| 32 B, k=m=3, sparse | 72.45 ± 1.17 | 99.06 ± 5.88 |
| 32 B, k=m=5, dense | 74.18 ± 2.19 | 94.70 ± 0.83 |
| 32 B, k=m=9, dense | 75.11 ± 3.87 | 99.80 ± 5.92 |
| 62 B, k=m=3, dense | 75.23 ± 5.23 | 97.89 ± 5.23 |
| 1 KiB, k=m=3, dense | 87.09 ± 2.16 | 104.24 ± 4.04 |

Reconstruction means and errors below are milliseconds. Percentages are ratios
of measured means; overlapping intervals limit the precision of small differences.

| Scenario | Plain VSS, ms | Authenticated, ms | Mean difference |
| --- | ---: | ---: | ---: |
| 8 B, k=m=3, dense | 0.517 ± 0.013 | 0.633 ± 0.041 | +22.58% |
| 32 B, k=m=2, dense | 0.682 ± 0.016 | 0.788 ± 0.049 | +15.60% |
| 32 B, k=m=3, dense | 1.024 ± 0.049 | 1.091 ± 0.021 | +6.60% |
| 32 B, k=m=3, sparse | 1.010 ± 0.019 | 1.122 ± 0.061 | +11.10% |
| 32 B, k=m=5, dense | 1.695 ± 0.085 | 1.793 ± 0.089 | +5.74% |
| 32 B, k=m=9, dense | 3.252 ± 0.182 | 3.121 ± 0.127 | −4.03%, unresolved |
| 62 B, k=m=3, dense | 1.005 ± 0.028 | 1.141 ± 0.091 | +13.60% |
| 1 KiB, k=m=3, dense | 25.489 ± 0.861 | 25.499 ± 0.586 | +0.04%, unresolved |

The signature layer adds verification work. The negative nine-share point estimate
in this primary run is measurement variation, not an optimization or a negative
authentication cost. Its intervals overlap, as do the primary 32-byte three/five
share and 1 KiB reconstruction intervals. The standalone authentication timings
isolate the added work more directly.

For single-share verification, the primary 32-byte dense means are 422.90→517.14
µs at k=2, 621.24→707.67 µs at k=3, 1120.95→1188.75 µs at k=5 and
2256.76→2383.87 µs at k=9. Sparse k=3 is 819.69→917.39 µs. The same signature
cost forms a larger fraction of cheaper VSS operations.

The 1 KiB reconstruction measurements had minimum iterations of 98.778 ms
(plain) and 99.199 ms (authenticated), just below BenchmarkDotNet's recommended
100 ms. Longer focused measurements for those cases and the noisy nine-share
pair are retained separately. No multimodal-distribution warning occurred in
the primary run. Standard outlier handling is recorded in the full log.

## Longer confirmation

The focused run uses two launches, 15 warmups, 15 measurement iterations,
300 ms target iteration and unroll factor one. It completed all four cases in
118.84 seconds, with **0 managed B/op, zero GC collections and no warnings**.

| Scenario | Plain reconstruction, ms | Authenticated reconstruction, ms |
| --- | ---: | ---: |
| 32 B, k=m=9, dense | 3.053 ± 0.089 | 3.032 ± 0.012 |
| 1 KiB, k=m=3, dense | 25.968 ± 0.653 | 25.753 ± 0.561 |

The intervals still overlap. The run removes the short-iteration concern but
does not resolve the additional authentication cost within the end-to-end timing
variation for these two scenarios. Neither negative difference is a speedup.
The isolated 95–104 µs authentication measurements remain the direct estimate of
the added verification work on this host. There is no claim of zero overhead.

The separate [confirmation JSON](confirmation/results/Mrbr.Cryptography.Vss.Authentication.Benchmarks.AuthenticationBenchmarks-report-full.json)
and [comparison CSV](confirmation-comparison.csv) retain these results. They are
not pooled with the primary run, because the jobs have different settings.

## Interpreting allocations and throughput

Zero B/op means zero managed bytes in measured warmed calls. This does not
measure native allocator traffic, cold native-library/runtime initialization,
key provisioning, caller buffer allocation or exceptions. Unit tests also check
zero managed allocations on invalid signatures, wrong contexts and invalid shares.

[operations.csv](operations.csv) contains all 48 methods/scenarios with normalized
microseconds, uncertainty, operations/second and GC/managed-allocation results.
[comparison.csv](comparison.csv) contains all 16 plain/authenticated pairs,
mean differences, throughput and interval-overlap indicators. Interval overlap
is an uncertainty indicator, not a formal paired statistical test. These are
machine-specific measurements; comparisons use the same run/backend/inputs.

## Reproduce

```powershell
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug

dotnet run --project benchmarks/Mrbr.Cryptography.Vss.Authentication.Benchmarks -c Release --no-build -- --filter '*' --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 120 --unrollFactor 1 --artifacts benchmarks/results/2026-11-04-vss-authenticated/artifacts

dotnet run --project benchmarks/Mrbr.Cryptography.Vss.Authentication.Benchmarks -c Release --no-build -- --filter '*Reconstruct*32B_k9*' '*Reconstruct*1024B*' --launchCount 2 --warmupCount 15 --iterationCount 15 --iterationTime 300 --unrollFactor 1 --artifacts benchmarks/results/2026-11-04-vss-authenticated/confirmation
```

BenchmarkDotNet restores/builds its generated executable, so NuGet access can
still be needed with the outer `--no-build` option. The pinned .NET 11 RC is
required by global.json. Full primary [JSON](artifacts/results/Mrbr.Cryptography.Vss.Authentication.Benchmarks.AuthenticationBenchmarks-report-full.json),
[Markdown](artifacts/results/Mrbr.Cryptography.Vss.Authentication.Benchmarks.AuthenticationBenchmarks-report-github.md),
[CSV](artifacts/results/Mrbr.Cryptography.Vss.Authentication.Benchmarks.AuthenticationBenchmarks-report.csv)
and [HTML](artifacts/results/Mrbr.Cryptography.Vss.Authentication.Benchmarks.AuthenticationBenchmarks-report.html)
reports are retained with the execution log. Metadata records source/native
fingerprints, job arguments, host details and verification results.
