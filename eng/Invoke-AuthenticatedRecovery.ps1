param(
    [ValidateSet('Test', 'Benchmark')]
    [string] $Action = 'Test',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $ServiceSourceRoot = 'C:/dev/source',
    [string] $Artifacts = 'BenchmarkDotNet.Artifacts/authenticated-recovery'
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$properties = @('-p:UseLocalKeyManager=true', '-p:ShouldUnsetParentConfigurationAndPlatform=false', "-p:MrbrServiceSourceRoot=$ServiceSourceRoot")
$previousSourceRoot = $env:MrbrServiceSourceRoot
Push-Location $repository
try {
    $env:MrbrServiceSourceRoot = $ServiceSourceRoot
    if ($Action -eq 'Test') {
        & "$PSScriptRoot/Invoke-Integration.ps1" -Action Test -Configuration $Configuration -ServiceSourceRoot $ServiceSourceRoot
    }
    else {
        if ($Configuration -ne 'Release') { throw 'Benchmarks require Release configuration.' }
        & dotnet build benchmarks/Mrbr.Cryptography.Vss.KeyManager.Benchmarks -c Release @properties -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Authenticated recovery benchmark build failed.' }
        & dotnet run --project benchmarks/Mrbr.Cryptography.Vss.KeyManager.Benchmarks -c Release @properties --no-build -- `
            --filter '*' --launchCount 1 --warmupCount 15 --iterationCount 12 --iterationTime 150 --unrollFactor 1 --artifacts $Artifacts
        if ($LASTEXITCODE -ne 0) { throw 'Authenticated recovery benchmarks failed.' }
    }
}
finally {
    $env:MrbrServiceSourceRoot = $previousSourceRoot
    Pop-Location
}
