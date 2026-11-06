using BenchmarkDotNet.Attributes;
using Mrbr.Cryptography.Internal;
using Mrbr.Cryptography.WeightMeasurements;

namespace Mrbr.Cryptography.WeightBenchmarks;

[MemoryDiagnoser, Config(typeof(WeightConfig))]
public class WeightsOnlyBenchmarks
{
    [Params(2, 3, 5, 9)] public int Count { get; set; }
    [Params(false, true)] public bool Sparse { get; set; }
    private byte[] coordinates = null!, oldWeights = null!, newWeights = null!;

    [GlobalSetup]
    public void Setup()
    {
        coordinates = WeightScenarios.Coordinates(Count, Sparse);
        oldWeights = new byte[Count];
        newWeights = new byte[Count];
        Original();
        Optimized();
        if (!oldWeights.AsSpan().SequenceEqual(newWeights)) throw new InvalidOperationException("Weights differ.");
    }

    [Benchmark(Baseline = true)]
    public byte Original()
    {
        // Original production loop, excluding public reconstruction validation
        // and payload operations. Both sides receive prevalidated coordinates.
        for (int i = 0; i < coordinates.Length; i++)
        {
            byte numerator = 1, denominator = 1;
            for (int j = 0; j < coordinates.Length; j++)
            {
                if (j != i)
                {
                    numerator = Gf256.Multiply(numerator, coordinates[j]);
                    denominator = Gf256.Multiply(denominator, (byte)(coordinates[i] ^ coordinates[j]));
                }
            }
            oldWeights[i] = Gf256.Divide(numerator, denominator);
        }
        return oldWeights[0];
    }

    [Benchmark]
    public byte Optimized()
    {
        LagrangeWeights.Calculate(coordinates, newWeights);
        return newWeights[0];
    }
}
