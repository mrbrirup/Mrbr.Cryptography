# External comparison setup

The normal solution, libraries, console examples and tests build without the
external comparison checkout. The two comparison benchmark projects are separate
from `Mrbr.Cryptography.slnx` and `Mrbr.Cryptography.Integration.slnx`.

`Comparison/` is local-only and excluded by the root `.gitignore`. The public
repository also excludes downloaded upstream source/documentation snapshots and
frozen upstream assemblies from the recorded comparison's `provenance/` directory.
It retains JSON manifests, our original harness snapshots, reports, logs and CSVs.
Manifest paths to excluded local files describe the original experiment; those
files will not exist in a fresh Git checkout until recreated separately.

## Obtain the measured revision

From the repository root, if the local comparison directory is not already present:

```powershell
git clone https://github.com/shinji-san/SecretSharingDotNet.git Comparison/SecretSharingDotNet-main
git -C Comparison/SecretSharingDotNet-main checkout bb0405a2bc954dc5a1c9fa77611e9969902cee1e
```

This is the SecretSharingDotNet 1.1.0 source revision used by the
[recorded experiment](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md).
Preserve its own licence. The source is not part of Mrbr.Cryptography's MIT grant.

## Reapply only the recorded .NET 11 upgrade

The retained reports describe a minimally retargeted local copy, not a different
sharing algorithm. Before running net11.0 verification or the head-to-head benchmark:

1. Append `net11.0` to `TargetFrameworks` in the upstream
   `src/SecretSharingDotNet.csproj` and `tests/SecretSharingDotNetTest.csproj`.
2. Include `net11.0` in the tests' existing `CsCheck` package-reference condition,
   alongside the modern target frameworks already listed there.
3. Restore/build those targets to generate their net11.0 lock-file entries.
   Retain the existing framework entries and dependency versions.

The recorded upgrade changed only these two project files and their two lock
files. It required no C# changes. The public JSON provenance includes the original
and changed hashes, but does not ship the ignored copies of the upstream files.

## Run the checks and comparisons

```powershell
./eng/Invoke-ShamirComparison.ps1 -Action Verify -ComparisonTargetFramework net11.0
```

The wrapper supports `Verify`, `Benchmark` and `Runtime` actions. Benchmark target
controls compile the required upstream target and recreate the local frozen
assemblies. The runtime control builds one net10.0 assembly for both runtime jobs;
it additionally needs the .NET 10 runtime and targeting pack.

Use a separate working copy when repeating the historical experiments. The wrapper
refreshes fixed provenance assembly paths as well as writing the requested report
directory. Changing `-Artifacts` alone does not preserve those local assembly paths.
The summariser's output path must match the dataset being analysed.

The [full report](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md)
contains exact historical commands and measurement settings. The
[performance article](blog-performance-and-benchmarks.md) explains allocation
boundaries and why the results do not establish a general .NET 11 speedup.

The other requested repository, stevehansen/ShamirSecretSharing, was reviewed for
functionality and was not benchmarked. Its downloaded source snapshots are excluded
too; the [functionality review](comparison-functionality.md) links its pinned source
directly on GitHub.
