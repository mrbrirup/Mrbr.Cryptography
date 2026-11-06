param(
    [ValidateSet('Build', 'Test', 'Benchmark')]
    [string] $Action = 'Test',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $ServiceSourceRoot = 'C:/dev/source',
    [string] $Artifacts = 'BenchmarkDotNet.Artifacts/integration'
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$properties = @('-p:UseLocalKeyManager=true', '-p:ShouldUnsetParentConfigurationAndPlatform=false', "-p:MrbrServiceSourceRoot=$ServiceSourceRoot")
# BDN's generated project inherits the selected sibling repository root.
$previousSourceRoot = $env:MrbrServiceSourceRoot
Push-Location $repository
try {
    $env:MrbrServiceSourceRoot = $ServiceSourceRoot
    & dotnet build Mrbr.Cryptography.Integration.slnx -c $Configuration @properties -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Integration build failed.' }
    if ($Action -eq 'Test') {
        & dotnet test --solution Mrbr.Cryptography.Integration.slnx -c $Configuration @properties --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
    }
    elseif ($Action -eq 'Benchmark') {
        if ($Configuration -ne 'Release') { throw 'Benchmarks require Release configuration.' }
        & dotnet run --project benchmarks/Mrbr.Cryptography.IntegrationBenchmarks -c Release @properties --no-build -- `
            --filter '*' --launchCount 1 --warmupCount 20 --iterationCount 15 --iterationTime 150 --unrollFactor 1 --artifacts $Artifacts
        if ($LASTEXITCODE -ne 0) { throw 'Integration benchmarks failed.' }
    }
}
finally {
    $env:MrbrServiceSourceRoot = $previousSourceRoot
    Pop-Location
}
