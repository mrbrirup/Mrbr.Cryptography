using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using Mrbr.Cryptography.Vss.OptimizationBenchmarks.Baselines;

namespace Mrbr.Cryptography.Vss.OptimizationBenchmarks;

public sealed class ComparisonConfig : ManualConfig
{
    public ComparisonConfig()
    {
        AddJob(Job.Default.DontEnforcePowerPlan().AsDefault());
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddExporter(JsonExporter.Full);
    }
}

public sealed record VerificationScenario(int Length, int Threshold, int Supplied, bool Sparse, bool FirstCoordinate = false)
{
    public override string ToString() => $"{Length}B_k{Threshold}_m{Supplied}_{(Sparse ? "Sparse" : "Dense")}{(FirstCoordinate ? "_x1" : "")}";
}

[MemoryDiagnoser, Config(typeof(ComparisonConfig))]
public class VerificationBenchmarks
{
    [ParamsSource(nameof(Scenarios))] public VerificationScenario Scenario { get; set; } = null!;
    public IEnumerable<VerificationScenario> Scenarios
    {
        get
        {
            foreach (int length in new[] { 8, 32 })
                foreach (int count in new[] { 2, 3, 5, 9 })
                    foreach (bool sparse in new[] { false, true }) yield return new(length, count, count, sparse);
            yield return new(32, 3, 5, false);
            yield return new(32, 3, 9, true);
            yield return new(62, 3, 3, false);
            yield return new(63, 3, 3, false);
            yield return new(32, 10, 10, true);
            yield return new(1024, 3, 3, false);
            yield return new(32, 3, 3, false, true);
            yield return new(32, 9, 9, false, true);
        }
    }

    private byte[] shares = null!, commitments = null!, original = null!, optimized = null!;
    private int shareLength;

    [GlobalSetup]
    public void Setup()
    {
        byte[] secret = new byte[Scenario.Length];
        for (int i = 0; i < secret.Length; i++) secret[i] = (byte)((i * 31 + 19) & 255);
        int count = Scenario.Sparse ? 255 : Scenario.Supplied;
        shareLength = PedersenVss.GetShareLength(secret.Length);
        byte[] all = new byte[PedersenVss.GetSharesLength(secret.Length, count)];
        commitments = new byte[PedersenVss.GetCommitmentsLength(secret.Length, Scenario.Threshold)];
        // Original creator makes this a wire-compatibility comparison as well.
        PedersenBaseline.CreateShares(secret, Scenario.Threshold, count, all, commitments);
        shares = new byte[shareLength * Scenario.Supplied];
        for (int i = 0; i < Scenario.Supplied; i++)
        {
            // Dense verification targets its largest coordinate; sparse starts
            // at 255. Both versions see exactly the same order and payloads.
            int coordinate = Scenario.Sparse ? (254 + 73 * i) % 255 + 1
                : Scenario.FirstCoordinate ? i + 1 : Scenario.Supplied - i;
            all.AsSpan((coordinate - 1) * shareLength, shareLength).CopyTo(shares.AsSpan(i * shareLength));
        }
        original = new byte[secret.Length]; optimized = new byte[secret.Length];
        if (!OriginalVerify() || !OptimizedVerify()) throw new InvalidOperationException("Share verification failed.");
        OriginalReconstruct(); OptimizedReconstruct();
        if (!secret.AsSpan().SequenceEqual(original) || !secret.AsSpan().SequenceEqual(optimized)) throw new InvalidOperationException("Recovery mismatch.");
        CryptographicOperations.ZeroMemory(all); CryptographicOperations.ZeroMemory(secret);
    }

    [Benchmark] public bool OriginalVerify() => PedersenBaseline.VerifyShare(shares.AsSpan(0, shareLength), commitments);
    [Benchmark] public bool OptimizedVerify() => PedersenVss.VerifyShare(shares.AsSpan(0, shareLength), commitments);
    [Benchmark] public int OriginalReconstruct() => PedersenBaseline.Reconstruct(shares, commitments, original);
    [Benchmark] public int OptimizedReconstruct() => PedersenVss.Reconstruct(shares, commitments, optimized);

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (byte[] buffer in new[] { shares, commitments, original, optimized }) CryptographicOperations.ZeroMemory(buffer);
    }
}
