# KeyManager and EncryptionManager integration measurements

All 49 cases completed on 2026-11-03 using SDK
`11.0.100-rc.3.26522.148`, .NET 11 RC3 x64, Windows 10 22H2 and an Intel
Core i7-10750H (6 cores / 12 logical processors), with AVX2 available.
BenchmarkDotNet `0.16.0-preview.2` used one launch, 20 warmup iterations,
15 measured iterations of 150 ms and unroll factor 1. Total run time was
388.62 seconds. The job retained the existing OS power plan.

The core sources and original 27-case benchmark harness were unchanged.
Core source hashes match the [saved SIMD baseline](../2026-11-03-simd/README.md).
The adapter comparisons below use direct calls to that same SIMD implementation
in the same integration run, rather than comparing unrelated historical timings.
No interpolation-weight or SIMD implementation changes are part of this increment.

All cases use three-of-five sharing. Creation includes real platform RNG calls.
`Source=0` is the actual Block KeyService; `Source=1` is the actual Matrix
KeyService. The fixtures use freshly generated test source material and validate
recovery/decryption before timing. Configuration acceptance, its history I/O,
buffer creation and fixture cleanup happen outside measurement.

## Eight-byte handle

| Operation | Direct SIMD core baseline | Handle adapter | Managed allocation |
| --- | ---: | ---: | ---: |
| Create five shares | 1.142 us | 1.076 us | 0 B/op |
| Recover from three shares | 0.746 us | 0.748 us | 0 B/op |

The creation confidence intervals overlap. This is evidence of comparable cost,
not evidence that adding the adapter improves core performance.

## Replay key material and share it

Means in microseconds; all 30 key-sharing cases reported **0 B/op** and no GC
collections. Creation compares a key already in a buffer with replaying that
same key into wiped stack scratch space and then sharing it.

| Source | Key bytes | Core create | Replay + adapter create | Core recover | Adapter recover |
| --- | ---: | ---: | ---: | ---: | ---: |
| Block | 16 | 0.576 | 0.575 | 0.498 | 0.530 |
| Block | 24 | 1.502 | 1.572 | 0.818 | 0.756 |
| Block | 32 | 0.695 | 0.777 | 0.524 | 0.526 |
| Matrix | 16 | 0.563 | 0.602 | 0.498 | 0.525 |
| Matrix | 24 | 1.473 | 1.604 | 0.769 | 0.847 |
| Matrix | 32 | 0.657 | 0.730 | 0.523 | 0.501 |

Key recovery uses reordered coordinates **5, 1, 3**, identically in both compared
methods. It does not access KeyManager. The additional
`RecoverHandleAndReplay` alternative reconstructs coordinates **1, 2, 3** of an
eight-byte handle and replays the requested key; it measured 0.767–0.835 us and
0 B/op across the six source/key-length combinations. This alternative has a
different payload and coordinate set and is not an isolated adapter-overhead
comparison. Its exact results are in the CSV/JSON reports.

The non-monotonic relationship between length and latency is expected from the
current implementation: 16 bytes fit a 128-bit SIMD block, 32 bytes fit an AVX2
block, while eight-byte payloads and the eight-byte remainder of a 24-byte key use
scalar arithmetic. Sharing a handle saves storage but does not automatically
minimize computation. The interpolation weights still use the original algorithm.

## AES-GCM decryption and initialization boundary

Plaintext is 1,024 bytes, with no additional associated data. KeyManager source
is Block. All methods authenticate the original handle, as required by the
existing EncryptionManager artifact format. Handle/key recovery in these cases
uses coordinates **1, 2, 3**. Means are in microseconds.

| Operation | AES-128 | AES-192 | AES-256 | Managed allocation |
| --- | ---: | ---: | ---: | ---: |
| Existing handle-based decryption | 1.106 | 1.142 | 1.231 | 40 B/op |
| Reuse initialized raw-key decryptor | 0.603 | 0.680 | 0.701 | **0 B/op** |
| Initialize raw-key context, decrypt, dispose | 1.260 | 1.450 | 1.375 | 112 B/op |
| Recover handle, existing decryption | 1.969 | 2.022 | 2.031 | 40 B/op |
| Recover key, initialize context, decrypt, dispose | 1.793 | 2.262 | 2.132 | 112 B/op |

Reusing an initialized context excludes key reconstruction, key import and
context disposal from each timed call. It therefore has a different lifetime
from one-shot handle decryption. AES-256 reuse delivered approximately 1.43
million decryptions per second for this 1 KiB fixture. The complete raw-key
recovery/decryption path is explicitly **not** zero allocation: initialization
allocates 112 managed bytes on this runtime. Import once and reuse while that key
remains appropriate. These are managed-memory measurements; native crypto
resource allocation is not counted by MemoryDiagnoser.

Across all 49 cases, 37 reported zero managed bytes per operation: all 34 sharing
cases and the three reused-decryptor cases. The remaining 12 include AES context
initialization: six reported 40 B/op and six reported 112 B/op. No successful
steady-state sharing/decryption path silently allocates a scratch array. Tests
also verify zero allocations with additional associated data up to 4 KiB using
caller-owned scratch space; the decryption timings above use empty additional data.

## Interpretation and reproduction

There were 13 multimodal-distribution warnings, plus reported outlier removals.
Several small adapter/core timing differences have overlapping 99.9% confidence
intervals. Treat these point estimates as measurements on this host, not precise
universal overhead or speedup guarantees. Raw reports retain confidence intervals,
individual samples, operations per second, GC data and warnings. Allocation counts
were also checked independently in the tests.

Release and Debug each passed **197 tests: 144 core + 53 integration**, with zero
build warnings or errors. Integration tests exercise actual Block/Matrix replay,
all AES sizes, keys up to 65,535 bytes, sparse/dense coordinates, source-registry
loss, corrupted shares, handle/nonce/tag/cipher/AAD/key tampering, overlap rejection,
workspace wiping, destination suffixes, disposal and warmed allocations.

Reproduce from the repository root, with the manager checkouts under
`C:/dev/source`:

```powershell
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug
./eng/Invoke-Integration.ps1 -Action Benchmark -Artifacts 'BenchmarkDotNet.Artifacts/integration'
```

The source-integration script selects the local KeyManager rather than its NuGet
package and preserves Release configuration through the sibling project graph.
Use `-ServiceSourceRoot` if the checkouts move. The manager working trees already
contained changes before this increment; their commit IDs, working-tree summaries
and actual source hashes are captured. No KeyManager sources or existing
EncryptionManager methods were changed by this integration increment.

Artifacts:

- [Combined 49-case CSV](comparison.csv)
- [Metadata, host information and source/report SHA-256 fingerprints](metadata.json)
- [Decryption report](artifacts/results/Mrbr.Cryptography.IntegrationBenchmarks.DecryptionBenchmarks-report-github.md)
- [Handle report](artifacts/results/Mrbr.Cryptography.IntegrationBenchmarks.HandleSharingBenchmarks-report-github.md)
- [Key sharing report](artifacts/results/Mrbr.Cryptography.IntegrationBenchmarks.KeySharingBenchmarks-report-github.md)
- Full JSON, CSV and HTML exports and the complete execution log are retained in
  [the artifacts directory](artifacts/).

See the [integration guide](../../../docs/key-manager-integration.md) for API
examples, byte encoding, polynomial evaluation, interpolation and the requirement
to retain the original handle as authenticated metadata for raw-key recovery.
