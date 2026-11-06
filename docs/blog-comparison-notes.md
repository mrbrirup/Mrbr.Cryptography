# Blog comparison notes

Suggested subject: **Shamir sharing on modern .NET: design, allocations and retargeting**.

The [companion performance article](blog-performance-and-benchmarks.md) expands
these notes with the optimisation sequence, allocation tables and runtime controls.

For 32-byte secrets in a three-of-five scheme, the recorded .NET 11 RC3 run measured
Mrbr creation at 0.599 µs and reconstruction at 0.060 µs.
SecretSharingDotNet's BigInteger backend measured 19.562 µs and 49.999 µs respectively:
mean time ratios of 32.7× and 831.7×. Mrbr measured zero
managed allocations in all 16 creation/reconstruction cases.

These are implementation/workload comparisons. The field model, object/buffer
ownership, cleanup and numeric backend differ. The same upstream binary on .NET
10 versus 11 gave mixed changes, including slower BigInteger cases on .NET 11;
retargeting on the same runtime also did not reproduce the large implementation
gap. Avoid attributing the whole ratio to .NET 11, claiming Span/SIMD are new in
.NET 11, asserting security superiority, or generalizing these key-sized workloads
to large documents. Quote the complete dataset, not only the fastest case.

Use the [full report](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md)
for uncertainty, scope, reproducible commands and CSVs, and the
[functionality review](comparison-functionality.md) for the pinned repository
revisions. The stevehansen project's current source includes optional HMAC share
authentication and rejection sampling despite older limitation text; describe the
selected API/version explicitly. No GitHub-wide popularity ranking was established.

Publication details to include: 4 November 2026; Intel i7-10750H/Windows x64;
.NET 11 RC3 SDK 11.0.100-rc.3.26522.148; .NET 10.0.14 for the runtime control;
BenchmarkDotNet 0.16.0-preview.2; one launch, 15 warmups, 12 measurements;
real RNG, repeated warmed buffers/objects, required cleanup included;
reconstruction fixtures regenerated per process, matching payload/field/coordinates.

NuGet packaging remains deferred. The library and derived applications continue
using project references. The self-describing raw-share envelope remains a future
proposal in [future development](future-development.md).
