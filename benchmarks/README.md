# ShamirSharedSecret benchmarks

This project measures the current public implementation and compares it with the
retained scalar baseline from before SIMD was added.
It targets the same pinned .NET 11 RC SDK as the library and uses BenchmarkDotNet
`0.16.0-preview.2`. Benchmark dependencies belong only to this project; the library
still has no package dependencies.

The separate [integration benchmark suite](../docs/key-manager-integration.md)
measures handle sharing, actual KeyManager replay plus sharing, and raw-key
decryption with initialization reported separately. Its
[recorded results](results/2026-11-03-integration/README.md) compare adapter calls
against direct calls to the unchanged SIMD core. The 27-case harness described
below is unchanged.

The [weight optimization suite](../docs/interpolation-weights.md) pairs a frozen
SIMD reconstruction baseline with the optimized public API, covering all supplied
share counts from two through nine with dense and sparse coordinates. It also
isolates weight setup. See the [recorded results](results/2026-11-03-weights/README.md).

The separate [VSS suite](../docs/pedersen-vss.md) measures Pedersen creation,
individual share verification and verified reconstruction, alongside the unchanged
non-verifying implementation. See the [VSS results](results/2026-11-03-vss/README.md).

The [VSS optimization suite](../docs/vss-verification-optimization.md) pairs the
frozen original VSS implementation with current verification/reconstruction on
identical shares, including sparse coordinates and fast-path boundaries. See
the [paired results](results/2026-11-03-vss-optimized/README.md).

The separate [authentication suite](../docs/authenticated-commitment-packets.md)
measures Ed25519 envelope creation/authentication and pairs plain VSS operations
with authenticated share verification/reconstruction on identical inputs. See
the [authentication results](results/2026-11-04-vss-authenticated/README.md).

The [authenticated key-recovery suite](../docs/authenticated-key-recovery.md)
uses the real KeyManager and EncryptionManager for replay, sharing/signing,
recovery and decryption. It measures decryptor initialization separately from
reused decryption and the fresh recovery/import/decrypt workflow. See the
[recorded comparison](results/2026-11-04-authenticated-recovery/README.md).

## Run

From the solution directory, outside a debugger:

```powershell
dotnet run --project benchmarks/Mrbr.Cryptography.Benchmarks --configuration Release -- --list flat
dotnet run --project benchmarks/Mrbr.Cryptography.Benchmarks --configuration Release -- --filter '*'
```

The second command uses BenchmarkDotNet's normal adaptive warmup and measurement
settings. A bounded baseline run uses:

```powershell
dotnet run --project benchmarks/Mrbr.Cryptography.Benchmarks --configuration Release -- --filter '*' --launchCount 1 --warmupCount 30 --iterationCount 15 --iterationTime 250 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/simd
```

BenchmarkDotNet builds a separate executable and can restore packages during that
build, so NuGet access may be needed even when the outer `dotnet run` uses
`--no-build`.

Thirty warmup iterations allow expensive cases with few calls per iteration to
stabilize before measurement. An initial six-warmup run showed a large timing
transition during the 1 MiB creation samples. The longer warmup is part of the
recorded baseline settings.

There are 27 cases. To select a method or scenario, use a filter such as
`--filter '*ReconstructThreshold*'` or `--filter '*65536B_3of5*'`.
`--job Dry` is useful for checking setup/builds, but its cold single sample is not
a throughput baseline.

## What is measured

| Method | Timed work |
| --- | --- |
| CreateShares | Validation, fresh platform cryptographic randomness, polynomial evaluation, output writes, and scratch wiping |
| ReconstructThreshold | Validation and interpolation from exactly k reordered shares |
| ReconstructAll | Validation and interpolation from all n shares |

`GlobalSetup` creates all buffers, generates reconstruction inputs, selects the
threshold subset, and checks that all measured paths recover the original bytes.
Creation and reconstruction use separate share buffers. The timed methods call
only the public API and return its byte count, which the harness consumes.
There is no per-iteration setup, buffer allocation, share selection, or input copying.
`GlobalCleanup` wipes the fixture buffers outside measurement.

The benchmark inputs are ordinary test data, never user secrets. Reconstruction
reuses valid shares generated during setup; creation obtains fresh entropy on
every invocation. This measures warmed operations on reused buffers, not cold
initialization, caller buffer allocation, serialization, or file operations.
The harness retains the current OS power plan; keep it consistent between runs.

## Scenario matrix

Scenario labels are `secretLengthB_thresholdofshareCount`.

| Secret bytes | k | n | Purpose |
| --- | --- | --- | --- |
| 32 | 3 | 5 | Key-sized secret |
| 1,024 | 3 | 5 | Small payload |
| 65,536 | 3 | 5 | Sustained buffer processing |
| 1,048,576 | 3 | 5 | Large payload |
| 65,536 | 2 | 3 | Low-degree polynomial |
| 65,536 | 5 | 10 | Higher threshold and output volume |
| 65,536 | 10 | 20 | More polynomial work per byte |
| 32 | 3 | 255 | Many shares with a low threshold |
| 32 | 255 | 255 | Maximum threshold and coordinate boundary |

This curated set avoids a large Cartesian product. Comparing the three-of-five
payload sizes shows fixed overhead versus per-byte cost. The other rows expose
creation's dependence on n(k-1) and reconstruction's dependence on the number of
supplied shares, including the cost of recomputing interpolation weights per call.

## Read the results

- `Mean`, `Error`, and `StdDev`: time per public API call and timing uncertainty.
- `Op/s`: completed API calls per second.
- `Allocated` and GC columns: managed allocations/collections for measured calls.
  A dash in `Allocated` means zero measured managed bytes per operation.
- `Secret MiB/s`: original secret bytes / mean operation time / 1,048,576. This
  uses the same numerator for creation and reconstruction regardless of k or n.
- `Share MiB/s`: creation's packed share bytes generated, or reconstruction's
  packed share bytes consumed, divided by mean operation time / 1,048,576.
  Includes the coordinate byte in each share. These are logical data rates,
  not a measurement of physical memory bandwidth.

For example, creating five shares of a 65,536-byte secret produces
`5 * (65,536 + 1) = 327,685` share bytes. Its share rate is about five times its
secret rate; it does not mean the original secret was processed five times faster.

BenchmarkDotNet exports CSV, Markdown, HTML, and full JSON, with runtime and machine
metadata, to the selected artifacts directory. Generated build trees and logs are
ignored by source control. Retained baseline reports live under `results/`.

The [3 November 2026 scalar baseline](results/2026-11-03-scalar/README.md) contains
all 27 results, raw measurements, allocation diagnostics, and source fingerprints.
The [SIMD comparison](results/2026-11-03-simd/README.md) retains the corresponding
optimized run and a matched comparison CSV. Benchmark source and job settings
remain identical for that comparison.

## Compare future optimizations

Keep SDK, runtime, checked-arithmetic settings, hardware, scenario matrix, and job
arguments the same. Run with other CPU-heavy work stopped, and compare error and
variance as well as means. This suite includes the real RNG cost in creation and
does not isolate GF multiplication. Any SIMD improvement should be assessed
against these end-to-end calls and must preserve the allocation results.

The initial measurements are a machine-specific baseline. Use the adaptive run
or additional launches to investigate noisy or borderline changes.

Tooling references: [BenchmarkDotNet execution](https://github.com/dotnet/performance/blob/main/docs/benchmarkdotnet.md),
[job settings](https://benchmarkdotnet.org/articles/configs/jobs.html), and
[exported reports](https://benchmarkdotnet.org/articles/configs/exporters.html).
