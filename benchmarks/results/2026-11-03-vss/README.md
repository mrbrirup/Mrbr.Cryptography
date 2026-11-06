# Pedersen VSS: creation, verification and reconstruction

All **40 benchmark cases** completed on 2026-11-03. Every case reported
**0 managed B/op and zero measured GC collections**, including both VSS and
the unchanged non-verifying implementation. Successful calls use caller-owned
buffers; runtime/native-library/RNG initialization happens during setup.

The VSS implementation is a separate `Mrbr.Cryptography.Vss` project using
Pedersen commitments over Ristretto255 through **libsodium 1.0.22**. It has no
reference to the original project. SHA-256 fingerprints of all seven original
core source/project files match the preceding weight-optimization increment.
No verification branch, inheritance, native dependency or alternate arithmetic
was added to the non-verifying library.

## VSS results

Means in **milliseconds**. Creation generates all n shares and the public
commitment packet with fresh randomness. Verification checks one complete share.
Reconstruction verifies and uses exactly k selected shares, checks byte decoding,
then writes the secret. All methods include their framing and canonical-value
checks, hashing, H derivation and scratch cleanup.

| Secret bytes | k of n | Create all shares | Verify one share | Verified reconstruction |
| --- | --- | ---: | ---: | ---: |
| 8 | 2 of 3 | 0.340 ms | 0.307 ms | 0.634 ms |
| 8 | 3 of 5 | 0.505 ms | 0.424 ms | 1.337 ms |
| 8 | 5 of 7 | 0.811 ms | 0.689 ms | 3.421 ms |
| 32 | 2 of 3 | 0.633 ms | 0.585 ms | 1.192 ms |
| 32 | 3 of 5 | 0.936 ms | 0.822 ms | 2.486 ms |
| 32 | 5 of 7 | 1.563 ms | 1.351 ms | 6.372 ms |
| 32 | 9 of 9 | 2.871 ms | 2.350 ms | 19.588 ms |
| 1,024 | 3 of 5 | 15.569 ms | 14.177 ms | 39.861 ms |

For the 32-byte, three-of-five case, throughput was approximately **1,068 sharing
operations/s**, **1,216 single-share verifications/s**, and **402 verified
reconstructions/s**. Each sharing operation creates five shares; these are not
individual-share creation rates.

VSS uses 31-byte data blocks. Eight bytes require one block, 32 bytes require
two, and 1,024 bytes require 34. More blocks add independent coefficient
commitments and verification equations. Raising k also adds curve operations to
each verification; verified reconstruction performs those checks for every
supplied share. Verification dominates the scalar interpolation work in these
cases. These costs are part of this protocol, even though managed allocation
remains zero.

## In-run non-verifying comparison

Means in **microseconds**. These methods use the original GF(256) implementation
with the same input lengths, thresholds, total counts and selected coordinates.
The two protocols have different fields, share sizes and security work; these
figures show their application costs, not an equivalent-security speed comparison.

| Secret bytes | k of n | Shamir creation | Shamir reconstruction |
| --- | --- | ---: | ---: |
| 8 | 2 of 3 | 0.437 us | 0.169 us |
| 8 | 3 of 5 | 1.052 us | 0.248 us |
| 8 | 5 of 7 | 2.358 us | 0.435 us |
| 32 | 2 of 3 | 0.250 us | 0.043 us |
| 32 | 3 of 5 | 0.600 us | 0.057 us |
| 32 | 5 of 7 | 0.866 us | 0.099 us |
| 32 | 9 of 9 | 1.108 us | 0.186 us |
| 1,024 | 3 of 5 | 4.561 us | 0.224 us |

Architectural isolation is established by unchanged core source/project hashes
and the absence of a VSS reference in that project. Exact timings across sessions
can vary. The [weight-optimization report](../2026-11-03-weights/README.md)
retains the prior paired reconstruction baseline and its uncertainty information.

## What is measured

`GlobalSetup` creates deterministic test payloads, generates valid reconstruction
inputs, selects a reordered threshold subset alternating high/low coordinates,
and checks exact recovery through both APIs. It warms native loading, libsodium,
the platform RNG and both managed implementations before measurement. Creation
destinations are separate from fixed reconstruction inputs, so timed creation
cannot invalidate the verification fixture.

The five measured methods are `ShamirCreate`, `VssCreate`, `VssVerifyOne`,
`ShamirReconstruct` and `VssVerifiedReconstruct`. Creation includes fresh RNG,
coefficient sampling and output hashes. VSS reconstruction validates the complete
packet once, verifies every selected share, computes scalar interpolation weights,
checks all recovered padding before writes, and performs a second scalar
interpolation pass to write without a plaintext-sized scratch array.
There is no unchecked VSS reconstruction method in the public API.

The 24 VSS and 16 non-verifying cases all reported zero managed allocation and
no GC collections. These measurements exclude fixture allocation, buffers,
initialization and failure exceptions. MemoryDiagnoser does not measure native
allocator traffic. The native DLL and NuGet package fingerprints are retained
in metadata; the tests assert that the loaded native API reports version 1.0.22.

## Tests, environment and uncertainty

Release and Debug each passed **352 tests**, with no build warnings/errors:

- 228 existing non-verifying core tests.
- 71 new VSS tests, including allocation checks on successful and non-throwing
  invalid-data paths, and RFC 9496 primitive vectors.
- 53 existing KeyManager/EncryptionManager integration tests.

VSS tests also use independent BigInteger modular arithmetic/interpolation,
deterministic polynomial expectations, sampling rejection boundaries, identity
points, all three-of-five subsets/permutations, sparse coordinates including 255,
maximum threshold, chunk edges, trailing zeros, malformed/noncanonical values,
modified value/blinding/commitment data, mixed sets, duplicates, insufficient
quorums, RNG failure cleanup and destination preservation on invalid byte decoding.

Host: Windows 10 22H2, Intel Core i7-10750H (6 cores / 12 logical processors),
.NET 11 RC3 x64, SDK `11.0.100-rc.3.26522.148`, native libsodium 1.0.22.
BenchmarkDotNet `0.16.0-preview.2` used one launch, 20 warmups, 15 measured
iterations of 150 ms, unroll factor 1, Release without a debugger. The existing
High performance power plan was retained. Total time was **313.1 seconds**.

BenchmarkDotNet reported **three multimodal-distribution warnings** and outliers,
with some samples removed by its normal policy. Means are host/workload-specific;
full 99.9% confidence intervals, samples and warnings are preserved. This run is
on Windows x64; other packaged native platforms have not been tested here.
Tests and vetted primitives do not constitute an independent protocol audit.

## Reproduce and inspect

From the repository root:

```powershell
dotnet build Mrbr.Cryptography.slnx -c Release
dotnet test --solution Mrbr.Cryptography.slnx -c Release --no-build
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release
./eng/Invoke-Integration.ps1 -Action Test -Configuration Debug

dotnet run --project benchmarks/Mrbr.Cryptography.Vss.Benchmarks -c Release --no-build -- --filter '*' --launchCount 1 --warmupCount 20 --iterationCount 15 --iterationTime 150 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/vss
```

The standalone solution runs 299 core/VSS tests. The integration script adds the
53 manager tests and needs the existing manager checkouts. Generated benchmark
builds may require NuGet access despite the outer `--no-build`.

- [All 40 timings, confidence margins, throughput and memory counts](comparison.csv)
- [Host, source/native-package fingerprints and verification metadata](metadata.json)
- [BenchmarkDotNet Markdown report](artifacts/results/Mrbr.Cryptography.Vss.Benchmarks.VssBenchmarks-report-github.md)
- [Full JSON/CSV/HTML and complete log](artifacts/)
- [Mathematics, format, API examples and trust requirements](../../../docs/pedersen-vss.md)

All implementation and benchmark source hashes remained unchanged during the run.
Historical scalar/SIMD/weight/integration reports retain their original evidence.
