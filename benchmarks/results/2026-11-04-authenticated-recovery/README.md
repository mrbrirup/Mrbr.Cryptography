# Authenticated AES recovery — 4 November 2026

The optional `Mrbr.Cryptography.Vss.KeyManager` adapter completes replay,
Pedersen sharing, Ed25519 commitment authentication, key recovery and raw-key
AES-GCM decryption. See the [workflow, mathematics and example](../../../docs/authenticated-key-recovery.md).

Release and Debug each pass **522 tests**, including **62 new recovery cases**.
Both builds have zero warnings/errors. The exact documentation example compiled
and recovered known plaintext using the real managers. Tests also remove the
original registry and prove recovery/decryption works while key replay fails.
Fifteen existing core/VSS/authentication source/project fingerprints are unchanged.
The manager sources were not edited; relevant external fingerprints are recorded.

## Environment and method

- Windows 10 22H2 x64; Intel Core i7-10750H, 6 cores/12 logical processors.
- SDK `11.0.100-rc.3.26522.148`, .NET 11 RC3 x64 RyuJIT, workstation GC.
- libsodium 1.0.22, BenchmarkDotNet 0.16.0-preview.2, Release, no debugger.
- Existing High performance power plan retained; no power-plan mutation.
- One launch, 15 warmups, 12 measurement iterations, 150 ms target, unroll one.
- Real Block/Matrix sources; AES-128/192/256; three-of-five sharing, plus
  AES-256 two-of-three and nine-of-nine Block scenarios.
- Cipher plaintext is 1,024 deterministic bytes. Original cipher AAD is
  `cipher-purpose/v1`; signed application context is
  `Mrbr.AuthenticatedRecovery/benchmark-key`; expected generation is 42.
- The exact threshold number of reordered shares is supplied, starting from the
  largest issued coordinate. All n shares are produced during creation.

The primary run completed 48 cases in **322.93 seconds** with no warnings.
The context-initialization correction below completed eight cases in **90.80
seconds**, also with no warnings. Standard outlier handling is in the raw logs.

Configuration, fixture I/O, source/key generation, caller buffers, encryption
and input checks in GlobalSetup are outside measured calls. Each process has
fresh source material and a generated handle. Direct/adapter comparisons match
source type, sizes, thresholds, operations and cleanup; they do not reuse identical
random source material across separate benchmark processes. Treat their mean
differences as observations on this host, not universal adapter costs/speedups.

## Allocation boundary

| Operation | Managed B/op |
| --- | ---: |
| Direct replay/share/sign composition | 0 |
| Adapter replay/share/sign | 0 |
| Authenticated key recovery | 0 |
| Reused recovered-key decryption | 0 |
| Initialize/dispose a context retained by benchmark consumer | 112 |
| Recover key, initialize context, decrypt and dispose | 113–117 reported |

All **32 cases** in the first four rows measure zero managed bytes and zero GC
collections. Creating a context remains an allocating initialization operation.
The fresh recovery/import/decrypt method actually imports the newly reconstructed
key on every call. Reused decryption imports a verified recovered key once during
setup, wipes the caller key, then decrypts repeatedly with that same context.
It does not reconstruct or import a fresh key on every reused decryption call.

For the fresh method, raw GC diagnostics show `112*N + 224` allocated bytes for
N measured operations in every scenario. The per-operation diagnostic is rounded
to 113–117 B/op because N ranges from 48 to 247. The additional 224 bytes is fixed
per diagnostic measurement here; this report does not assign its origin to a
particular production call. The initialization measurement directly reports
112 B/op. Both paths allocate; neither is presented as zero allocation.

Native allocator traffic is not measured. Initialization, caller buffers and
exception paths are outside the warmed zero-allocation contract. Unit tests also
measure zero managed bytes on signature-rejection recovery paths.

## Creation comparison

Direct composition replays through IKeyService.GetKey, writes the recovery
context, calls PedersenVss.CreateShares, signs with AuthenticatedCommitmentPacket
and wipes key/commitment/context scratch. The adapter performs those operations
and its complete parameter/overlap validation with one caller workspace.

Times are means ± half the BenchmarkDotNet 99.9% confidence interval, in µs.

| Scenario | Direct composition | Adapter | Mean difference |
| --- | ---: | ---: | ---: |
| Block AES-128, 3/5 | 536.09 ± 17.17 | 525.39 ± 4.09 | −2.00% |
| Block AES-192, 3/5 | 522.87 ± 5.47 | 533.13 ± 13.25 | +1.96% |
| Block AES-256, 2/3 | 700.22 ± 33.17 | 677.85 ± 15.88 | −3.19% |
| Block AES-256, 3/5 | 1079.22 ± 47.93 | 1005.64 ± 20.65 | −6.82% |
| Block AES-256, 9/9 | 2912.43 ± 158.35 | 2819.75 ± 41.23 | −3.18% |
| Matrix AES-128, 3/5 | 525.76 ± 3.36 | 580.20 ± 29.97 | +10.35% |
| Matrix AES-192, 3/5 | 530.23 ± 15.28 | 563.13 ± 22.47 | +6.21% |
| Matrix AES-256, 3/5 | 1071.92 ± 70.45 | 1015.69 ± 41.08 | −5.25% |

Six of eight interval pairs overlap. Block AES-256 3/5 is lower and Matrix
AES-128 3/5 is higher without overlap in this run. No general performance win
is claimed for this integration. The adapter adds a defined authenticated key
identity and consistent validation/cleanup while retaining zero managed allocations.
[comparison.csv](comparison.csv) includes throughput, all means/errors and overlap
indicators. Interval overlap is not a formal paired statistical test.

## Recovery and cipher workflow

Recovery and fresh-workflow times are ms; reused 1 KiB decryption is µs.
Independent benchmark means should not be subtracted to infer a tiny constructor
cost inside a much slower VSS operation.

| Scenario | Recover key, ms | Reused decrypt, µs | Recover/import/decrypt, ms |
| --- | ---: | ---: | ---: |
| Block AES-128, 3/5 | 0.602 ± 0.007 | 0.582 ± 0.004 | 0.625 ± 0.024 |
| Block AES-192, 3/5 | 0.606 ± 0.013 | 0.623 ± 0.004 | 0.611 ± 0.016 |
| Block AES-256, 2/3 | 0.777 ± 0.023 | 0.652 ± 0.018 | 0.774 ± 0.026 |
| Block AES-256, 3/5 | 1.081 ± 0.014 | 0.639 ± 0.008 | 1.080 ± 0.017 |
| Block AES-256, 9/9 | 3.121 ± 0.137 | 0.663 ± 0.029 | 3.271 ± 0.162 |
| Matrix AES-128, 3/5 | 0.604 ± 0.008 | 0.609 ± 0.025 | 0.614 ± 0.027 |
| Matrix AES-192, 3/5 | 0.641 ± 0.028 | 0.622 ± 0.007 | 0.624 ± 0.021 |
| Matrix AES-256, 3/5 | 1.077 ± 0.014 | 0.658 ± 0.023 | 1.150 ± 0.092 |

AES-128/192 keys fit one 31-byte VSS block; AES-256 needs two. Recovery scales
primarily with VSS block/quorum work, not ciphertext length. Reused AES-GCM
decryption scales with ciphertext length. Only one ciphertext size is measured
here, so no broader cipher throughput scaling claim is made.

## Initialization correction

The original initialization-only method created and immediately discarded a
context and reported 80 B/op. A revised method disposes and then returns that
context to BenchmarkDotNet's consumer, keeping its object observable. It reports
**112 B/op** for all eight cases. The 32-byte difference is consistent with
elimination of the discarded wrapper object in the first form. This is a benchmark
correction; production code, recovery/decryption methods and tests did not change.

Corrected initialization/disposal means are 0.555–0.572 µs. Both original and
corrected measurements are retained, with the original benchmark source snapshot
in [RecoveryBenchmarks.primary.cs.txt](RecoveryBenchmarks.primary.cs.txt). Use
the corrected figures for a context that will be retained for use. Returning an
already disposed context here prevents native resource leaks in the benchmark.
The production workflow retains a live context until decryption is finished.

## Evidence and reproduction

- [operations.csv](operations.csv): effective 48-case table, using corrected
  initialization rows, with times, errors, operations/second, allocations and GC.
- [primary-operations.csv](primary-operations.csv): original 48 cases, unaltered.
- [initialization-operations.csv](initialization-operations.csv): eight corrected cases.
- [Full primary JSON](artifacts/results/Mrbr.Cryptography.Vss.KeyManager.Benchmarks.RecoveryBenchmarks-report-full.json)
  and [Markdown](artifacts/results/Mrbr.Cryptography.Vss.KeyManager.Benchmarks.RecoveryBenchmarks-report-github.md).
- [Full corrected initialization JSON](initialization/results/Mrbr.Cryptography.Vss.KeyManager.Benchmarks.RecoveryBenchmarks-report-full.json)
  and [Markdown](initialization/results/Mrbr.Cryptography.Vss.KeyManager.Benchmarks.RecoveryBenchmarks-report-github.md).
- Both folders also retain CSV, HTML and execution logs.
- [metadata.json](metadata.json): SDK/native hashes, source snapshots, source
  fingerprints, job settings, external source configuration and test evidence.

```powershell
./eng/Invoke-AuthenticatedRecovery.ps1 -Action Test -Configuration Release
./eng/Invoke-AuthenticatedRecovery.ps1 -Action Test -Configuration Debug
./eng/Invoke-AuthenticatedRecovery.ps1 -Action Benchmark -Artifacts BenchmarkDotNet.Artifacts/authenticated-recovery
```

The wrapper selects the sibling source projects and preserves Release through
the generated benchmark build. Use `-ServiceSourceRoot` when checkouts move.
It uses the repository's pinned .NET 11 RC and may need NuGet network access for
generated restores. A future full run uses the corrected initialization method.
