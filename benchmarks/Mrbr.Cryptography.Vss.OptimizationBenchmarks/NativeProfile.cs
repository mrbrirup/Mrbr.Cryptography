using System.Diagnostics;
using Mrbr.Cryptography.Vss.OptimizationBenchmarks.Baselines;

namespace Mrbr.Cryptography.Vss.OptimizationBenchmarks;

// Bounded diagnostic to choose the public-coordinate cutoff before the paired
// BenchmarkDotNet run. This does not measure managed allocations or API latency.
internal static class NativeProfile
{
    internal static void Run()
    {
        byte[] point = new byte[32], result = new byte[32], scalar = new byte[32];
        RistrettoBaseline.DeriveH(point);
        Measure("encoded point addition", () => SodiumBaseline.PointAdd(result, point, point));
        Measure("derive H", () => RistrettoBaseline.DeriveH(result));
        foreach (byte coordinate in new byte[] { 1, 2, 3, 5, 7, 9, 15, 255 })
        {
            scalar[0] = coordinate;
            Measure($"full scalar multiply x={coordinate}", () => RistrettoBaseline.MultiplyPublicPoint(scalar, point, result));
        }
    }

    private static void Measure(string name, Action action)
    {
        for (int i = 0; i < 50; i++) action();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < 300; i++) action();
        Console.WriteLine($"{name}: {Stopwatch.GetElapsedTime(start).TotalMicroseconds / 300:F2} us");
    }
}
