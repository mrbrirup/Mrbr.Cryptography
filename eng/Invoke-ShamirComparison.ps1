param(
    [ValidateSet('Verify', 'Benchmark', 'Runtime')]
    [string] $Action = 'Verify',
    [ValidateSet('netstandard2.0', 'net10.0', 'net11.0')]
    [string] $ComparisonTargetFramework = 'net11.0',
    [string] $Artifacts = 'BenchmarkDotNet.Artifacts/secretsharingdotnet',
    [switch] $TargetControlSubset
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$previousTarget = $env:ComparisonTargetFramework
$previousFrozen = $env:ComparisonUseFrozenAssembly
Push-Location $repository
try {
    if ($Action -eq 'Runtime') {
        & dotnet build Comparison/SecretSharingDotNet-main/src/SecretSharingDotNet.csproj -c Release -f net10.0 -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Runtime-control subject build failed.' }
        $frozenDirectory = Join-Path $repository 'benchmarks/results/2026-11-04-secretsharingdotnet/provenance/runtime-control'
        New-Item -ItemType Directory -Path $frozenDirectory -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repository 'Comparison/SecretSharingDotNet-main/src/bin/Release/net10.0/SecretSharingDotNet.dll') -Destination (Join-Path $frozenDirectory 'SecretSharingDotNet.dll')
        Copy-Item -LiteralPath (Join-Path $repository 'Comparison/SecretSharingDotNet-main/LICENSE.md') -Destination (Join-Path $frozenDirectory 'LICENSE.md')
        & dotnet build benchmarks/Mrbr.Cryptography.RuntimeComparison -c Release -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Runtime-control build failed.' }
        & dotnet run --project benchmarks/Mrbr.Cryptography.RuntimeComparison -c Release --no-build -- `
            --filter '*' --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 150 --unrollFactor 1 --artifacts $Artifacts
        if ($LASTEXITCODE -ne 0) { throw 'Runtime comparison failed.' }
    }
    else {
        $env:ComparisonTargetFramework = $ComparisonTargetFramework
        $useFrozen = if ($Action -eq 'Benchmark' -and $ComparisonTargetFramework -ne 'net11.0') { 'true' } else { 'false' }
        $env:ComparisonUseFrozenAssembly = $useFrozen
        if ($useFrozen -eq 'true') {
            & dotnet build Comparison/SecretSharingDotNet-main/src/SecretSharingDotNet.csproj -c Release -f $ComparisonTargetFramework -m:1
            if ($LASTEXITCODE -ne 0) { throw 'Target-control subject build failed.' }
            $targetDirectory = Join-Path $repository "benchmarks/results/2026-11-04-secretsharingdotnet/provenance/target-$ComparisonTargetFramework"
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $repository "Comparison/SecretSharingDotNet-main/src/bin/Release/$ComparisonTargetFramework/SecretSharingDotNet.dll") -Destination (Join-Path $targetDirectory 'SecretSharingDotNet.dll')
            Copy-Item -LiteralPath (Join-Path $repository 'Comparison/SecretSharingDotNet-main/LICENSE.md') -Destination (Join-Path $targetDirectory 'LICENSE.md')
        }
        & dotnet build benchmarks/Mrbr.Cryptography.ComparisonBenchmarks -c Release "-p:ComparisonTargetFramework=$ComparisonTargetFramework" "-p:ComparisonUseFrozenAssembly=$useFrozen" -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Comparison build failed.' }
        if ($Action -eq 'Verify') {
            & dotnet run --project benchmarks/Mrbr.Cryptography.ComparisonBenchmarks -c Release --no-build -- --verify
            if ($LASTEXITCODE -ne 0) { throw 'Public API verification failed.' }
        }
        else {
            $filters = if ($TargetControlSubset) { @('*Upstream*32B_3of5*', '*Upstream*32B_9of9*', '*Upstream*128B_3of5*') } else { @('*') }
            & dotnet run --project benchmarks/Mrbr.Cryptography.ComparisonBenchmarks -c Release --no-build -- `
                --filter @filters --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 150 --unrollFactor 1 --artifacts $Artifacts
            if ($LASTEXITCODE -ne 0) { throw 'Library comparison failed.' }
        }
    }
}
finally {
    $env:ComparisonTargetFramework = $previousTarget
    $env:ComparisonUseFrozenAssembly = $previousFrozen
    Pop-Location
}
