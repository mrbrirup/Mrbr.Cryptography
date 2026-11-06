# Shamir sharing on modern .NET: optimisation, allocations and the runtime question

Companion to the [project overview](blog-project-overview.md), prepared on
**5 November 2026** from measurements recorded on 3–4 November 2026. This is an
editable blog foundation. The results concern the pinned .NET 11 RC3 build, not
a subsequent final release. Links to local evidence need published repository
URLs when the article is published. No benchmarks were rerun to write this article.

Mrbr.Cryptography was built around caller-owned buffers, finite-field arithmetic
and explicit allocation boundaries. SIMD and interpolation optimisations produced
substantial measured gains within that implementation. A later comparison with
SecretSharingDotNet also found large differences in time and managed allocations
for small binary secrets.

The experiments support a more specific claim than attributing those differences
to the newest runtime: **implementation design mattered greatly in these
workloads; retargeting and runtime changes gave smaller, mixed results**.

## Start with an allocation boundary

The raw sharing APIs accept `ReadOnlySpan<byte>` and `Span<byte>`. Callers own
the input, share storage and reconstruction destination, and can reuse those
buffers. Scratch buffers have explicit bounds independent of payload length.
Creation still consumes fresh platform cryptographic randomness, and private
scratch is cleared in exception-safe cleanup paths.

This arrangement permits warmed operations with zero managed allocation. It also
makes ownership visible: the caller remains responsible for allocating, retaining
and clearing its own buffers. A span is a view of memory, not a guarantee that
the application which supplies that memory never allocates.
[Microsoft's Span documentation](https://learn.microsoft.com/en-us/dotnet/api/system.span-1?view=net-10.0)
describes views over managed, native and stack memory.

**Allocated bytes per operation is not peak memory usage.** It does not report
the full caller buffer footprint or the native allocations of the libsodium-based
layers. Initialisation, text conversion, console output and exception paths also
sit outside the warmed allocation claim. Memory and GC diagnostics were collected
with [BenchmarkDotNet's MemoryDiagnoser](https://benchmarkdotnet.org/articles/configs/diagnosers.html).
Avoiding managed allocations does not establish constant-time execution.

## Optimisation one: vectorise the payload arithmetic

The scalar starting point evaluates a polynomial for each secret byte using
Horner's rule. For a threshold of three, for example:

```text
s + a1*x + a2*x^2 = (a2*x + a1)*x + s
```

The arithmetic is in GF(256), so addition is XOR. Multiplication by a fixed field
element is linear. Split a payload byte into two nibbles and calculate:

```text
c*b = LowTable[b & 15] XOR HighTable[b >> 4]
LowTable[n]  = c*n
HighTable[n] = c*(n << 4)
```

The table products use field arithmetic. Each table has 16 entries and can be
held in vector registers. Register shuffles select the entries for many payload
bytes at once. The multiplier is a public share coordinate during creation or a
public interpolation weight during reconstruction; secret nibbles select register
entries rather than indexing an ordinary memory table.

AVX2 handles 32 bytes at once, a 128-bit path handles 16, and scalar code handles
tails or unsupported hardware. Creation groups each bounded batch of polynomial
coefficients into planes for parallel evaluation. This preserves the original
assignment of random coefficients to secret bytes.

For a 64 KiB secret, threshold three, five issued shares, the historical matched
scalar/SIMD experiment measured the following. Times are microseconds per call;
the ratio is scalar time divided by SIMD time.

| Operation | Scalar mean ± error, µs | SIMD mean ± error, µs | Time ratio | Managed B/op, both |
| --- | ---: | ---: | ---: | ---: |
| Create five shares | 6,163.417 ± 93.330 | 298.395 ± 19.993 | 20.66× | 0 |
| Reconstruct from three | 1,706.923 ± 40.024 | 13.546 ± 0.592 | 126.01× | 0 |
| Reconstruct from five | 3,039.324 ± 178.406 | 24.285 ± 2.815 | 125.15× | 0 |

Both sides measured 0 managed B/op. Creation included the real RNG. These are
results from the [3 November SIMD experiment](../benchmarks/results/2026-11-03-simd/README.md),
whose jobs used one launch, 30 warmups, 15 measurements and a 250 ms target
iteration. Six multimodal-distribution warnings were reported in that SIMD run.
Large improvements are clear, but small timing differences need more evidence.

There was no resolved improvement when reconstructing tiny secrets from all 255
shares. Computing interpolation weights still dominated those calls. Accelerating
the payload loop did not remove that setup cost.

## Optimisation two: reduce interpolation-weight setup

For `m` supplied shares and `L` secret bytes, reconstruction first computes `m`
coordinate-dependent weights, then applies them to the payload. The general
work remains `O(m^2 + L*m)`. Weight setup can be prominent for eight-byte handles
and 16/24/32-byte keys, even when the payload arithmetic is already vectorised.

The weight of share i at zero is:

```text
w_i = product(j != i) x_j / product(j != i) (x_i XOR x_j)
secret = XOR_i (w_i * y_i)
```

The optimised helper uses exact finite-field logarithm/exponent tables for these
public values. Multiplication/division become integer exponent addition/subtraction
modulo 255. These are discrete field representations, not floating-point logarithms.
Pairwise coordinate differences are shared, two- and three-share cases have
specialised formulas, and the identity `XOR_i w_i = 1` supplies the final weight.

The immutable tables occupy 766 bytes of assembly data. Their indices depend
only on validated public coordinates. Secret payload arithmetic retains its
existing scalar/vector paths; there is no coordinate-set dictionary or private
data cache. The [weight guide](interpolation-weights.md) contains the derivation.

In a separate paired experiment, 32-byte reconstruction with dense coordinates
measured as follows. Times here are **nanoseconds**, including validation, setup
and payload reconstruction. Both sides use the same SIMD payload kernels.

| Supplied shares | SIMD baseline mean ± error, ns | Optimised mean ± error, ns | Time ratio | Managed B/op, both |
| --- | ---: | ---: | ---: | ---: |
| 2 | 269.33 ± 3.99 | 40.91 ± 1.53 | 6.58× | 0 |
| 3 | 459.70 ± 6.78 | 60.85 ± 2.60 | 7.56× | 0 |
| 5 | 1,046.36 ± 125.13 | 102.64 ± 8.24 | 10.19× | 0 |
| 9 | 2,600.94 ± 117.31 | 187.80 ± 7.70 | 13.85× | 0 |

Both sides again measured 0 managed B/op. Across the measured dense and sparse
two- through nine-share cases, 32-byte reconstruction improved 6.30–13.85 times.
The isolated weight helper improved 27.26–47.27 times, which is a different
measurement from complete reconstruction. Eight-byte reconstruction improved
2.44–4.04 times; unchanged scalar payload work limits its total improvement.

Smaller inputs do not necessarily have lower latency. Eight-byte payloads and
eight-byte tails take scalar paths, while complete 16/32-byte blocks can use SIMD.
The [48 matched comparisons](../benchmarks/results/2026-11-03-weights/comparison.csv)
include dense and sparse coordinates, larger counts and regression checks.

Here `m` is the number of supplied shares, not necessarily the original threshold.
Every supplied share still participates. The API and share format are unchanged.
These paired measurements must not be multiplied by ratios from other historical
runs to manufacture a cumulative speedup estimate.

## Pedersen optimisation is a separate experiment

Pedersen VSS performs group operations and verifies commitments, so its work is
different from raw Shamir reconstruction. Its optimisations were kept in the VSS
project. They store the same fixed public generator H as immutable data, use
short addition chains for selected public coordinates, and verify interpolated
coefficient commitments for small quorums.

The coefficient checks are an exact change of basis of the original verification
equations. They do not replace the equations with a probabilistic batch check or
discard invalid extra shares. That path covers two through nine supplied shares
and at most 62 secret bytes; larger cases retain a fallback. Private scalars do
not use the public-coordinate addition-chain helper.

For 32-byte secrets with matching dense quorums, the paired results below use
**milliseconds**. k is the committed threshold and m the supplied share count.

| k/m | Original mean ± error, ms | Optimised mean ± error, ms | Time ratio | Managed B/op, both |
| --- | ---: | ---: | ---: | ---: |
| 3/3 | 2.441 ± 0.043 | 0.991 ± 0.021 | 2.46× | 0 |
| 9/9 | 20.317 ± 1.084 | 2.982 ± 0.040 | 6.81× | 0 |

All 96 original/optimised cases measured 0 managed B/op, with native allocation
traffic outside that measurement. Individual share-verification gains varied by
coordinate, and some cases had overlapping confidence intervals. The
[complete VSS report](../benchmarks/results/2026-11-03-vss-optimized/README.md)
and [equivalence proof](vss-verification-optimization.md) retain those details.
Ed25519 authentication has its own [overhead measurements](../benchmarks/results/2026-11-04-vss-authenticated/README.md).
Neither VSS nor signatures are enabled in the raw-Shamir comparison below.

## Choosing and preserving the external baseline

The external subject was SecretSharingDotNet 1.1.0 at commit
`bb0405a2bc954dc5a1c9fa77611e9969902cee1e`. Before modification, all 150 files in
the supplied local source copy matched that upstream revision. Its
[pinned README](https://github.com/shinji-san/SecretSharingDotNet/blob/bb0405a2bc954dc5a1c9fa77611e9969902cee1e/README.md)
documents its broader numeric and framework support.

The requested .NET 11 upgrade changed two project files and two generated lock
files. It added the library/test target and the existing test-package condition;
all C# source and existing framework dependency entries were preserved. No C#
compatibility fix was required. Our production implementation was not changed
or derived from the comparison source.

The recorded correctness checks passed 407 standalone Mrbr tests, 1,963 upstream
tests and 432 public-API backend round trips across the three upstream targets.
The upstream run excluded Timing and Stress categories and intentionally skipped
one opt-in 2 GiB test. These checks validate the exercised behaviour, rather than
constituting a security audit or a survey of every sharing library.

The measured backends were:

- Mrbr's byte-wise GF(256) implementation with caller-owned buffers.
- SecretSharingDotNet's BigInteger backend with extended Euclidean reconstruction.
- Its SecureBigInteger backend with the recommended fixed-iteration reconstructor.

The upstream implementation represents the encoded whole secret as a number
under a selected Mersenne prime. Mrbr represents independent bytes in GF(256).
Ownership, numeric representation and cleanup differ. The secure backend also
has pinned-buffer and multipass-wiping contracts. Matching the payload and quorum
does not make these identical internal workloads or equivalent timing guarantees.

## Three questions require three experiments

| Question | What changes | What is held fixed | Cases |
| --- | --- | --- | ---: |
| Implementation comparison | Mrbr / upstream BigInteger / upstream secure backend | Both target and run on .NET 11 RC3 | 48 |
| Target-compilation control | Upstream netstandard2.0 / net10.0 / net11.0 target | Execution on .NET 11 RC3; matching methods/scenarios | 12 + 12 controls; matched net11 cases above |
| Runtime control | Execution on .NET 10.0.14 / .NET 11 RC3 | Frozen upstream net10.0 subject DLL; workload definitions | 16 |

The target and runtime controls cover both upstream backends. Each benchmark
process records the loaded subject's target and SHA-256; runtime jobs verify the
same frozen net10.0 subject DLL. A generated project-reference build initially
selected the wrong target. The setup guard rejected it before accepted timing;
successful controls pin the required assemblies. Rejected attempts are retained
and excluded from the result tables.

The four successful runs total **88 cases**. They used Windows 10 22H2 x64,
an Intel i7-10750H with AVX2, SDK `11.0.100-rc.3.26522.148`, .NET 11 RC3,
.NET 10.0.14 for the runtime control, BenchmarkDotNet `0.16.0-preview.2`,
Release and workstation GC. Each job used one launch, 15 warmups, 12 measurements,
a 150 ms target iteration and unroll one. Slow calls can exceed that target.
Reported statistics use the recorded BenchmarkDotNet outlier handling; the raw
measurements and execution logs are retained.

Creation includes fresh production randomness. Fixture allocation, first
initialisation and share selection occur outside timing. Creation produces all
n shares; reconstruction uses exactly k shares at the largest issued coordinates.
Every fixture checks the exact recovered bytes and length, including leading
and trailing zero bytes, before measurement.

The timed public operations include cleanup: Mrbr clears created shares and
recovered output; upstream result/secret objects and byte-export buffers are
disposed. Splitter/reconstructor instances are reused. Text parsing to create
independently owned input-share fixtures happens only during setup. No text
serialisation, encryption, storage or network I/O is timed. The
[public-API adapter](../benchmarks/Mrbr.Cryptography.ComparisonBenchmarks/UpstreamSubject.cs)
and [benchmark methods](../benchmarks/Mrbr.Cryptography.ComparisonBenchmarks/ComparisonBenchmarks.cs)
make that scope reviewable.

## Both implementations on .NET 11 RC3

For a 32-byte secret, threshold three and five issued shares, the measurements
below include the same operation boundary just described. **Time is microseconds**;
error is the reported half-width of the mean's 99.9% confidence interval.
Operations/s is the reciprocal mean, not an independent service-load measurement.

| Implementation / operation | Mean ± error, µs | Managed B/op | Operations/s |
| --- | ---: | ---: | ---: |
| Mrbr: create | 0.599 ± 0.009 | 0 | 1,669,311.2 |
| Mrbr: reconstruct | 0.0601 ± 0.0004 | 0 | 16,633,534.4 |
| Upstream BigInteger: create | 19.562 ± 0.627 | 22,974 | 51,119.5 |
| Upstream BigInteger: reconstruct | 49.999 ± 1.183 | 54,960 | 20,000.4 |
| Upstream secure: create | 756.660 ± 50.906 | 21,192 | 1,321.6 |
| Upstream secure: reconstruct | 185,019.416 ± 697.103 | 8,076,064 | 5.4 |

Relative to the upstream BigInteger backend, the unrounded means give Mrbr
time ratios of **32.7 times for creation and 831.7 times for reconstruction**.
Both libraries target and execute on .NET 11 here. The ratio therefore compares
their implementations, not .NET 10 against .NET 11.

The secure backend's larger cost is shown explicitly because it is a different
supported configuration. The experiment does not isolate how much time belongs
to fixed-iteration inversion, allocation, wiping or other arithmetic, nor does it
rank the implementations' security. Read its absolute timing and allocation
figures with its selected inversion and cleanup contracts.

Across all eight scenarios, the BigInteger-to-Mrbr mean time ratios were:

| Scenario | Creation ratio | Reconstruction ratio |
| --- | ---: | ---: |
| 8 B, 3-of-5 | 15.5× | 166.9× |
| 16 B, 3-of-5 | 39.5× | 784.4× |
| 24 B, 3-of-5 | 13.7× | 168.9× |
| 32 B, 2-of-3 | 46.8× | 734.4× |
| 32 B, 3-of-5 | 32.7× | 831.7× |
| 32 B, 5-of-7 | 39.7× | 842.9× |
| 32 B, 9-of-9 | 60.3× | 1,412.8× |
| 128 B, 3-of-5 | 22.2× | 992.3× |

All **16 Mrbr creation/reconstruction cases measured 0 managed B/op and zero
GC collections**. The full [library comparison CSV](../benchmarks/results/2026-11-04-secretsharingdotnet/library-comparison.csv)
contains every mean, error, throughput, allocation value and both backend pairs.
The cases are small-secret workloads, not evidence of whole-document throughput.

For throughput, count the bytes that match the question. Secret throughput uses
original payload bytes; raw-share throughput uses `n*(L+1)` bytes produced by
creation or `m*(L+1)` bytes consumed by reconstruction. The share rate is naturally
larger because it counts multiple copies of the payload-sized share. Neither rate
is a direct measurement of physical memory bandwidth. These warmed repeated-buffer
microbenchmarks also do not establish cold-start or application tail latency.

## Retargeting the library on the same runtime

.NET Standard specifies an API contract across implementations. It is not an
execution runtime. Supporting old targets does not force a modern consumer to
execute on .NET Framework. See [Microsoft's .NET Standard explanation](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
and [NuGet's framework selection reference](https://learn.microsoft.com/en-us/nuget/reference/target-frameworks#net-standard).

The original upstream project already targets .NET 8, 9 and 10 as well as older
targets, and has modern conditional code paths. This experiment deliberately
forced netstandard2.0 and net10.0 subject builds to execute on .NET 11, alongside
the added net11.0 build. It measures target/API/conditional-compilation effects;
it does not compare an old .NET Framework runtime with .NET 11.

For the 32-byte three-of-five BigInteger case, time is microseconds and both
allocation columns are managed bytes per operation:

| Compiled subject target | Create mean ± error, µs | Create B/op | Reconstruct mean ± error, µs | Reconstruct B/op |
| --- | ---: | ---: | ---: | ---: |
| netstandard2.0 | 19.481 ± 0.803 | 23,263 | 50.511 ± 1.208 | 54,992 |
| net10.0 | 20.736 ± 1.824 | 22,974 | 51.133 ± 1.125 | 54,960 |
| net11.0 | 19.562 ± 0.627 | 22,974 | 49.999 ± 1.183 | 54,960 |

Across the twelve net10.0-to-net11.0 matched cases, measured time changes ranged
from -10.35% to +10.07%; ten of twelve mean-confidence-interval pairs overlap.
For netstandard2.0-to-net11.0, eleven of twelve pairs overlap. The changes are
mixed and do not reproduce the cross-library ratios. Some allocation differences
also arise between compiled targets without changing the execution runtime.
The [target-control CSV](../benchmarks/results/2026-11-04-secretsharingdotnet/target-comparison.csv)
retains all 24 matched comparisons. Interval overlap is a descriptive flag, not
a formal equivalence test.

## The same upstream binary on .NET 10 and .NET 11

The runtime control holds the upstream net10.0 DLL byte-for-byte fixed. Each
process records the same SHA-256, retained in [summary.json](../benchmarks/results/2026-11-04-secretsharingdotnet/summary.json).
Both runtimes execute the same defined payload/quorum workloads. The table uses
microseconds; positive change means .NET 11 took longer in this run.

| Payload | Operation/backend | .NET 10 mean ± error, µs | .NET 11 mean ± error, µs | Time change | Mean CIs overlap? |
| --- | --- | ---: | ---: | ---: | --- |
| 32 B | BigInteger create | 16.744 ± 0.566 | 18.491 ± 0.443 | +10.44% | No |
| 32 B | BigInteger reconstruct | 42.906 ± 3.357 | 50.412 ± 2.628 | +17.49% | No |
| 32 B | Secure create | 773.688 ± 74.469 | 737.106 ± 44.346 | -4.73% | Yes |
| 32 B | Secure reconstruct | 187,722.422 ± 1,640.281 | 182,043.529 ± 696.740 | -3.03% | No |
| 128 B | BigInteger create | 21.392 ± 0.358 | 24.441 ± 1.185 | +14.25% | No |
| 128 B | BigInteger reconstruct | 70.004 ± 4.147 | 70.321 ± 4.021 | +0.45% | Yes |
| 128 B | Secure create | 2,477.653 ± 33.795 | 2,327.792 ± 23.492 | -6.05% | No |
| 128 B | Secure reconstruct | 763,818.077 ± 53,605.839 | 817,212.921 ± 43,860.673 | +6.99% | Yes |

All four BigInteger point estimates are higher on .NET 11. Some secure-backend
operations improve. For example, 32-byte BigInteger reconstruction takes 17.49%
more time, while 128-byte secure creation takes 6.05% less time.

Allocation and timing can move in different directions. In the 32-byte cases:

| Operation/backend | .NET 10 managed B/op | .NET 11 managed B/op |
| --- | ---: | ---: |
| BigInteger create | 23,647 | 22,974 |
| BigInteger reconstruct | 56,888 | 54,960 |
| Secure create | 21,192 | 21,192 |
| Secure reconstruct | 8,076,064 | 8,076,064 |

BigInteger reconstruction allocates 56,888 B/op on .NET 10 and 54,960 B/op on
.NET 11, yet its measured mean time increases. Fewer allocated bytes alone do
not imply a faster complete operation. The secure backend's managed allocation
counts are unchanged in these cases. The full [runtime CSV](../benchmarks/results/2026-11-04-secretsharingdotnet/runtime-comparison.csv)
also contains the 128-byte allocation results.

There is an important limit to the control: reconstruction shares are randomly
created in each process. Payloads, fields, coordinates and quorum definitions
match, but the private share values are not frozen between jobs. Different
values can affect variable-time arithmetic and numeric allocation patterns, and
one launch does not separate that variation from every runtime or environmental
effect. The same fixture limitation applies across the target-control processes.
Non-overlapping mean intervals do not remove this limitation. The measurements establish these run
results, not a universal .NET 11 regression or speedup.

## What the .NET 11 results support

The strongest evidence for optimisation comes from comparisons within our own
implementation: retained scalar/SIMD baselines, unchanged SIMD kernels during
weight optimisation, and original/optimised VSS paths with identical fixed inputs.
Those changes directly target measured work while preserving protocol behaviour.

For the external comparison, a supported statement is:

> A caller-buffer, byte-field design with SIMD and specialised interpolation
> produced much lower measured time and managed allocation in these small-secret
> workloads. Retargeting the existing comparison library and changing its runtime
> were measured separately and produced smaller, mixed changes.

Choosing .NET 11 as the project's initial target keeps its supported target
surface small and permits a consistent modern implementation. That is a design
choice, not a measurement assigning the observed speedup to .NET 11. Span and
hardware-intrinsic APIs predate .NET 11; the earlier
[AVX2 API documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.x86.avx2.shuffle?view=net-10.0)
is one concrete example. Upstream's modern targets already use spans in relevant
paths. Earlier modern .NET releases are not proven incapable of the same design.

We have also not measured a .NET 10/11 comparison of our own implementation.
The runtime-control numbers apply to the frozen upstream subject. They cannot
be transferred directly to Mrbr or used to predict .NET 11 performance generally.

Before generalising small differences, repeat with more launches, reproducibly
fixed reconstruction fixtures and intended deployment hardware. Retain raw
samples and errors, and investigate timing components rather than attributing
every gap to GC. The [BenchmarkDotNet statistics documentation](https://benchmarkdotnet.org/articles/features/statistics.html)
discusses repeatability and how benchmark settings affect the observed results.
Windows x64 is the measured platform; ARM64 and other operating systems remain
separate validation work.

## Evidence and reproduction

All tables in this article are derived from retained CSV data. The
[complete comparison report](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md)
includes exact reproduction commands, workload scope and rejected-run history.
[Metadata](../benchmarks/results/2026-11-04-secretsharingdotnet/metadata.json)
records the selected upstream tests, source fingerprints, subject identities,
runtime details and artifact hashes. The
[functionality review](comparison-functionality.md) explains the projects'
different supported features; the second requested GitHub repository was reviewed
for functionality, not included in these timings.

The comparison wrapper is [Invoke-ShamirComparison.ps1](../eng/Invoke-ShamirComparison.ps1),
and [Summarize-ShamirComparison.py](../eng/Summarize-ShamirComparison.py) produces
the normalised comparisons after validating case counts, subject identities,
measurements and Mrbr allocations. Use a separate working copy to repeat the
recorded experiments: the wrapper refreshes frozen-assembly provenance paths as
well as writing the selected benchmark artifact directory. The summariser's
recorded output path must match the dataset being analysed.

Historical optimisation runs have their own job settings and evidence; they
are not pooled with the external comparison. The source-level changes, allocation
boundaries, cleanup contracts and measurement scope should accompany any quoted
headline figures. These results compare exercised performance, not security
strength or operational suitability for every application.
