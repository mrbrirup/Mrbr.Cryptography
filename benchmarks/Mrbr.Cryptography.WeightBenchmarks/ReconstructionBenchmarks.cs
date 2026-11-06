using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using Mrbr.Cryptography.WeightBenchmarks.Baselines;
using Mrbr.Cryptography.WeightMeasurements;

namespace Mrbr.Cryptography.WeightBenchmarks;

public sealed record ReconstructionScenario(int Length, int Count, bool Sparse)
{
    public override string ToString() => $"{Length}B_{Count}_{(Sparse ? "Sparse" : "Dense")}";
}

[MemoryDiagnoser, Config(typeof(WeightConfig))]
public class ReconstructionBenchmarks
{
    [ParamsSource(nameof(Scenarios))] public ReconstructionScenario Scenario { get; set; } = null!;
    public IEnumerable<ReconstructionScenario> Scenarios
    {
        get
        {
            foreach (int length in new[] { 8, 32 })
                for (int count = 2; count <= 9; count++)
                    foreach (bool sparse in new[] { false, true }) yield return new(length, count, sparse);
            foreach (int length in new[] { 16, 24 })
                foreach (bool sparse in new[] { false, true }) yield return new(length, 3, sparse);
            yield return new(32, 32, true);
            yield return new(32, 255, false);
            yield return new(32, 255, true);
            yield return new(65536, 3, true);
        }
    }

    private byte[] shares = null!, baselineOutput = null!, optimizedOutput = null!;

    [GlobalSetup]
    public void Setup()
    {
        shares = WeightScenarios.Shares(Scenario.Length, Scenario.Count, Scenario.Sparse);
        baselineOutput = new byte[Scenario.Length];
        optimizedOutput = new byte[Scenario.Length];
        OriginalSimd();
        Optimized();
        if (!baselineOutput.AsSpan().SequenceEqual(optimizedOutput))
            throw new InvalidOperationException("Baseline and optimized interpolation differ.");
    }

    [Benchmark(Baseline = true)]
    public int OriginalSimd() => SimdBaseline.Reconstruct(shares, Scenario.Length + 1, Scenario.Count, baselineOutput);
    [Benchmark]
    public int Optimized() => ShamirSharedSecret.Reconstruct(shares, Scenario.Length + 1, Scenario.Count, optimizedOutput);

    [GlobalCleanup]
    public void Cleanup()
    {
        CryptographicOperations.ZeroMemory(shares);
        CryptographicOperations.ZeroMemory(baselineOutput);
        CryptographicOperations.ZeroMemory(optimizedOutput);
    }
}
