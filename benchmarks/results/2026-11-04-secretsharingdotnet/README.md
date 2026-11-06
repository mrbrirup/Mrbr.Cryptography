# Shamir implementation, target-framework and runtime comparison

Recorded 4 November 2026. This compares the existing Mrbr raw Shamir library
with the supplied SecretSharingDotNet 1.1.0 source copy. It also separates target
compilation and runtime effects. NuGet packaging remains on hold; the libraries
continue to be developed with project references. No Mrbr production code is
changed or derived from the comparison source.

For a public Git checkout, third-party sources, copied upstream documentation and
frozen upstream assemblies are intentionally excluded. Reports, CSVs, JSON
provenance manifests and our harness snapshots remain available. References in
metadata to excluded paths describe the original local experiment. See the
[comparison setup guide](../../../docs/comparison-setup.md) to recreate local inputs.

## Provenance and correctness

SecretSharingDotNet commit `bb0405a2bc954dc5a1c9fa77611e9969902cee1e` is the
reviewed main branch revision. All 150 local files exactly matched its Git blob
hashes before the requested upgrade. We added net11.0 to library/test targets
and to the tests' existing CsCheck package condition. The two generated lock files
add net11 entries without changing existing framework dependency entries. These
four files are the complete third-party change set; all C# files are unchanged.

The net11 upstream suite passed **1,963 tests**, with one intentional 2 GiB buffer
test skipped; Timing and Stress categories were excluded. Our standalone suite
passed **407 tests**. An independent public-API harness checked 72 input/quorum
combinations against each of the upstream netstandard2.0, net10.0 and net11.0
builds, using both numeric backends: **432 successful backend round trips**.
These include all-zero, all-255 and mixed byte inputs at lengths 1/8/16/24/32/128
and thresholds 2/3/5/9. Every measured fixture checks exact reconstructed bytes
and length before timing. This is functional validation, not a security audit.

The [functionality review](../../../docs/comparison-functionality.md) separately
examines both requested GitHub projects. The stevehansen repository is reviewed
for features, not benchmarked here. Its current HMAC wrapper and rejection sampling
must not be overlooked by relying only on its older README/threat-model limitations.

## Experimental design

One cross-library ratio cannot identify the effect of choosing .NET 11. Three
experiments answer different questions:

1. **Implementation comparison:** both libraries target and run on .NET 11 RC3.
   Mrbr GF(256), upstream BigInteger/extended Euclid, and upstream
   SecureBigInteger/fixed-iteration reconstructor are measured separately.
   Eight scenarios × six methods = 48 cases.
2. **Target compilation control:** the upstream original net10.0 and
   netstandard2.0 builds run on the same .NET 11 runtime and use the same harness
   operations. Each control has 12 cases: both backends, creation/reconstruction,
   at 32-byte 3/5, 32-byte 9/9 and 128-byte 3/5. The matching net11 cases come
   from experiment one. This includes upstream conditional-compilation/API paths;
   it is not a runtime comparison.
3. **Runtime control:** the exact same frozen net10.0 upstream DLL runs under
   .NET 10 and .NET 11. A hash in every process verifies binary identity. Both
   backends and operations, 32/128-byte 3/5, two runtimes = 16 cases. This is
   the relevant control for runtime effects on this existing implementation.

The head-to-head and normal API probes use project references. BenchmarkDotNet's
generated dependency graph initially selected net11 despite a forced project
reference to net10; the fixture's target assertion rejected that attempt.
Successful target controls therefore pin freshly compiled, unmodified upstream
DLLs. The runtime control pins one net10 DLL for both runtimes. This is deliberate
experimental isolation, not a change to the application's project-reference workflow.
Rejected attempts are retained under `rejected/` and are excluded from tables.

.NET Standard specifies available APIs; it is not an execution runtime. A library
targeting it can benefit from a newer runtime, while target-dependent code can
still use different APIs. Supporting old targets alongside modern targets does
not automatically force modern consumers onto the old implementation.
[Microsoft .NET Standard documentation](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)

## Work and ownership included

Creation starts with raw bytes and produces all n shares. Upstream includes
Secret<T> construction and disposal of its returned shares/secret. Splitter and
reconstructor instances are reused. Mrbr uses caller-owned buffers and clears its
created shares after each call. This compares complete public byte-oriented
operations and cleanup, not isolated polynomial kernels.

Reconstruction uses exactly k shares at the same largest-issued coordinates,
exports original bytes into a caller buffer, and clears that output after each
call. Upstream's result and byte-export buffers are disposed. Text parsing to
make independently owned input-share copies happens only during setup, retains
the recorded field exponent, and is not included in reconstruction timings.

Inputs are a deterministic binary sequence with the first and last byte set to
zero, ensuring length/zero preservation is checked. Scenarios are 8/16/24/32/128
bytes at 3/5, plus 32 bytes at 2/3, 5/7 and 9/9. Random coefficients use each
library's real production RNG. Input/caller buffer allocation, first initialization,
share selection and correctness checks are outside timing. No text/Base64
serialization, network/file I/O, encryption, VSS or signatures are timed.

The cleanup contracts differ. Upstream pinned buffers use multipass secure
wiping, including its deliberately unoptimized wipe implementation. Mrbr uses
CryptographicOperations.ZeroMemory. The arithmetic differs too: Mrbr uses a
polynomial per byte in GF(256); upstream encodes an entire secret as a number
under an automatically selected Mersenne prime. Its exponent is reported by each
fixture and must not be confused with cipher security strength. Upstream can
support many more coordinates and numeric/security configurations than this
single-digit binary workload measures.

The secure upstream backend is paired with its recommended fixed-iteration
reconstructor, not with a faster variable-iteration alternative. Its documented
timing guarantees remain partial. These choices and allocation/ownership differences
are part of the measured implementation comparison; they are not attributed to
the runtime version or presented as equivalent security properties.

## Environment

Windows 10 22H2 x64; Intel Core i7-10750H, 6 cores/12 logical processors, AVX2.
SDK `11.0.100-rc.3.26522.148`; BenchmarkDotNet `0.16.0-preview.2`; Release,
workstation GC, no debugger. Exact runtime builds are in JSON/logs. The installed
High performance power plan is retained without modification. Every job uses
one launch, 15 warmups, 12 measurements, 150 ms target iteration and unroll one.
Slower calls may exceed the target iteration duration.

Reconstruction shares are freshly generated during each process's setup. Their
payload, field size, threshold and coordinate choices match, while their random
share values are not frozen across processes. This and one launch per case limit
how precisely small changes can be attributed. The runtime control fixes the
subject binary and workload definition; it is not a proof that every observed
small difference is caused exclusively by the runtime.

Builds of the downloaded upstream archive emit two SourceLink warnings because
there is no Git working directory. No upstream source warnings are suppressed or
algorithms changed to obtain a successful build. Benchmark warnings/uncertainty
are reported with the results rather than treated as application guarantees.

## Results: both implementations on .NET 11 RC3

All 48 head-to-head cases completed. All **16 Mrbr creation/reconstruction cases measured 0 managed B/op and zero GC collections**. The four successful experiments total **88 cases**. No BenchmarkDotNet warnings were reported in the successful runs.

For the preselected **32-byte, three-of-five** scenario, times are µs and error is half the 99.9% confidence interval. Creation includes output cleanup/disposal; reconstruction includes byte export and output cleanup.

| Implementation / operation | Mean ± error, µs | Managed B/op | Operations/s |
| --- | ---: | ---: | ---: |
| Mrbr GF(256): create | 0.599 ± 0.009 | 0 | 1,669,311.2 |
| Mrbr GF(256): reconstruct | 0.0601 ± 0.0004 | 0 | 16,633,534.4 |
| Upstream BigInteger: create | 19.562 ± 0.627 | 22,974 | 51,119.5 |
| Upstream BigInteger: reconstruct | 49.999 ± 1.183 | 54,960 | 20,000.4 |
| Upstream secure backend: create | 756.660 ± 50.906 | 21,192 | 1,321.6 |
| Upstream secure backend: reconstruct | 185019.416 ± 697.103 | 8,076,064 | 5.4 |

The secure backend includes its recommended fixed-iteration inverse and pinned-memory/multipass-cleanup contracts. Its large cost is reported separately; this is not a security-equivalence or security-ranking claim. These measurements do not isolate how much of that time belongs to inversion, allocation or wiping.

The complete comparison against the upstream BigInteger backend follows. Ratios divide its mean time by the Mrbr mean time; they describe this workload, not a runtime-version speedup. All original means/errors, the secure-backend pairs and throughput are in [library-comparison.csv](library-comparison.csv).

| Scenario | Operation | Mrbr, µs | Upstream BigInteger, µs | Time ratio |
| --- | --- | ---: | ---: | ---: |
| 128B_3of5 | Create | 1.121 | 24.835 | 22.2× |
| 128B_3of5 | Reconstruct | 0.073 | 72.572 | 992.3× |
| 16B_3of5 | Create | 0.489 | 19.308 | 39.5× |
| 16B_3of5 | Reconstruct | 0.062 | 48.998 | 784.4× |
| 24B_3of5 | Create | 1.362 | 18.720 | 13.7× |
| 24B_3of5 | Reconstruct | 0.295 | 49.753 | 168.9× |
| 32B_2of3 | Create | 0.248 | 11.593 | 46.8× |
| 32B_2of3 | Reconstruct | 0.047 | 34.505 | 734.4× |
| 32B_3of5 | Create | 0.599 | 19.562 | 32.7× |
| 32B_3of5 | Reconstruct | 0.060 | 49.999 | 831.7× |
| 32B_5of7 | Create | 0.825 | 32.733 | 39.7× |
| 32B_5of7 | Reconstruct | 0.104 | 87.673 | 842.9× |
| 32B_9of9 | Create | 1.105 | 66.650 | 60.3× |
| 32B_9of9 | Reconstruct | 0.191 | 269.723 | 1,412.8× |
| 8B_3of5 | Create | 1.008 | 15.619 | 15.5× |
| 8B_3of5 | Reconstruct | 0.262 | 43.699 | 166.9× |

## Results: retargeting the upstream library

All these builds execute on .NET 11 RC3. The net10→net11 changes range from -10.35% to +10.07% in measured time; ten of twelve confidence-interval pairs overlap. The forced netstandard2.0 control likewise gives small, mixed timing changes, with eleven of twelve interval pairs overlapping. None reproduces the cross-implementation ratios.

For 32-byte 3/5, using the BigInteger backend:

| Compiled upstream target | Create, µs | Reconstruct, µs | Execution runtime |
| --- | ---: | ---: | --- |
| .NET Standard 2.0 | 19.481 | 50.511 | .NET 11 RC3 |
| .NET 10 | 20.736 | 51.133 | .NET 11 RC3 |
| .NET 11 | 19.562 | 49.999 | .NET 11 RC3 |

The .NET Standard target is forced for this experiment; normal modern consumption of the multi-targeted library can select a modern compatible target. The built library already has NET8_0_OR_GREATER branches. Merely retaining old target frameworks does not imply those old code paths run in its modern builds. [target-comparison.csv](target-comparison.csv) records all 24 matched target-control pairs with allocations and interval indicators.

## Results: same binary, different runtime

The exact frozen net10 subject SHA-256 is `102797E5D1FBC33C4917B6DBFADD3AD5D636EA27D03E28F2997D434A757F8ABC` under both .NET 10.0.14 and .NET 11 RC3. Workload definitions are held fixed; the fresh-share fixture limitation above still applies.

Times are µs. Positive change means .NET 11 took longer in this run.

| Payload | Operation/backend | .NET 10 mean ± error | .NET 11 mean ± error | Time change | Intervals overlap |
| --- | --- | ---: | ---: | ---: | --- |
| 128 B | BigCreate | 21.392 ± 0.358 | 24.441 ± 1.185 | +14.25% | False |
| 128 B | BigReconstruct | 70.004 ± 4.147 | 70.321 ± 4.021 | +0.45% | True |
| 128 B | SecureCreate | 2477.653 ± 33.795 | 2327.792 ± 23.492 | -6.05% | False |
| 128 B | SecureReconstruct | 763818.077 ± 53605.839 | 817212.921 ± 43860.673 | +6.99% | True |
| 32 B | BigCreate | 16.744 ± 0.566 | 18.491 ± 0.443 | +10.44% | False |
| 32 B | BigReconstruct | 42.906 ± 3.357 | 50.412 ± 2.628 | +17.49% | False |
| 32 B | SecureCreate | 773.688 ± 74.469 | 737.106 ± 44.346 | -4.73% | True |
| 32 B | SecureReconstruct | 187722.422 ± 1640.281 | 182043.529 ± 696.740 | -3.03% | False |

The runtime results are mixed. All four BigInteger time estimates are higher on .NET 11 in this run; some secure-backend estimates improve. For example, 32-byte BigInteger reconstruction is 17.49% slower, while 128-byte secure creation is 6.05% faster. This dataset does **not** support a general claim that upgrading the runtime improves every sharing operation. See [runtime-comparison.csv](runtime-comparison.csv) for allocations and exact ratios.

## What the blog can support

A defensible account is: designing the Mrbr binary API around caller-owned buffers, byte-field arithmetic and SIMD for a single modern target produced large measured gains in these small-secret workloads. Retargeting an existing library and changing its runtime were measured separately and produced much smaller, mixed changes. The measurements do not assign the full library-to-library ratio to .NET 11.

Span and SIMD APIs are not exclusive to .NET 11. The current upstream modern targets already use spans in relevant paths. The comparison does not prove an earlier modern .NET target could not implement the same design, nor that a fast implementation is more secure. State the CPU, RC3 build, payload/quorum, cleanup scope, backend choices, one-launch limit and fresh-share fixtures when quoting the numbers. Repeat on intended hardware and with more launches before generalizing small differences.

Earlier [scalar](../2026-11-03-scalar/README.md), [SIMD](../2026-11-03-simd/README.md) and [weight-optimization](../2026-11-03-weights/README.md) measurements isolate improvements within our implementation more directly. They are separate historical experiments, not pooled with this dataset.

Normalized per-method data: [net11](net11-operations.csv), [net10 target](net10-target-operations.csv), [netstandard target](netstandard-target-operations.csv), and [runtime jobs](runtime-operations.csv). GC columns in those normalized files are raw diagnostic collection counts. [summary.json](summary.json) retains loaded targets/binary hashes and host details. Each run folder retains full benchmark reports and logs.


## Reproduce

From the Mrbr.Cryptography directory, using the pinned SDK:

```powershell
./eng/Invoke-ShamirComparison.ps1 -Action Verify -ComparisonTargetFramework net11.0
./eng/Invoke-ShamirComparison.ps1 -Action Verify -ComparisonTargetFramework net10.0
./eng/Invoke-ShamirComparison.ps1 -Action Verify -ComparisonTargetFramework netstandard2.0

./eng/Invoke-ShamirComparison.ps1 -Action Benchmark -ComparisonTargetFramework net11.0 -Artifacts benchmarks/results/2026-11-04-secretsharingdotnet/net11
./eng/Invoke-ShamirComparison.ps1 -Action Benchmark -ComparisonTargetFramework net10.0 -TargetControlSubset -Artifacts benchmarks/results/2026-11-04-secretsharingdotnet/net10-target
./eng/Invoke-ShamirComparison.ps1 -Action Benchmark -ComparisonTargetFramework netstandard2.0 -TargetControlSubset -Artifacts benchmarks/results/2026-11-04-secretsharingdotnet/netstandard-target
./eng/Invoke-ShamirComparison.ps1 -Action Runtime -Artifacts benchmarks/results/2026-11-04-secretsharingdotnet/runtime

python eng/Summarize-ShamirComparison.py
```

Archive prior output before repeating these recorded paths, or select new artifact
directories and update the summarizer's OUT accordingly. Generated benchmark
projects restore NuGet dependencies even when the outer run uses --no-build.
The runtime experiment also needs the installed .NET 10 runtime and targeting pack.

The upstream test executable was built for net11.0 and invoked with
`-trait- Category=Timing -trait- Category=Stress`; its
[full test XML](upstream-tests-net11.xml) is retained. Source/package provenance,
the minimally modified project/lock files, frozen control assemblies and licenses
are recorded in [metadata.json](metadata.json) and `provenance/`.

Each successful run folder retains full JSON, CSV, HTML, Markdown and execution
logs; the associated `*.console.log` files include generated build commands.
The summarizer validates loaded target identities, byte-identical runtime-control
subjects, case counts, completed measurements and Mrbr allocation results before
producing comparison CSVs. Machine-specific results with one launch do not prove
universal performance or cryptographic superiority.
