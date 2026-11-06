using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;

namespace Mrbr.Cryptography.Vss.Benchmarks;

public sealed class VssConfig : ManualConfig
{
    public VssConfig()
    {
        AddJob(Job.Default.DontEnforcePowerPlan().AsDefault());
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddExporter(JsonExporter.Full);
    }
}

public sealed record VssScenario(int Length, int Threshold, int Count)
{
    public override string ToString() => $"{Length}B_{Threshold}of{Count}";
}

[MemoryDiagnoser, Config(typeof(VssConfig))]
public class VssBenchmarks
{
    [ParamsSource(nameof(Scenarios))] public VssScenario Scenario { get; set; } = null!;
    public IEnumerable<VssScenario> Scenarios =>
    [
        new(8, 2, 3), new(8, 3, 5), new(8, 5, 7),
        new(32, 2, 3), new(32, 3, 5), new(32, 5, 7), new(32, 9, 9),
        new(1024, 3, 5)
    ];

    private byte[] secret = null!, vssCreation = null!, commitmentCreation = null!, vssSelected = null!, commitments = null!, vssRecovered = null!;
    private byte[] plainCreation = null!, plainSelected = null!, plainRecovered = null!;
    private int vssShareLength, plainShareLength;

    [GlobalSetup]
    public void Setup()
    {
        secret = new byte[Scenario.Length];
        for (int i = 0; i < secret.Length; i++) secret[i] = (byte)((i * 31 + 19) & 255);
        vssShareLength = PedersenVss.GetShareLength(secret.Length);
        plainShareLength = ShamirSharedSecret.GetShareLength(secret.Length);
        vssCreation = new byte[PedersenVss.GetSharesLength(secret.Length, Scenario.Count)];
        commitmentCreation = new byte[PedersenVss.GetCommitmentsLength(secret.Length, Scenario.Threshold)];
        commitments = new byte[commitmentCreation.Length];
        byte[] allVss = new byte[vssCreation.Length];
        PedersenVss.CreateShares(secret, Scenario.Threshold, Scenario.Count, allVss, commitments);
        vssSelected = Select(allVss, vssShareLength);
        CryptographicOperations.ZeroMemory(allVss);
        plainCreation = new byte[ShamirSharedSecret.GetSharesLength(secret.Length, Scenario.Count)];
        ShamirSharedSecret.CreateShares(secret, Scenario.Threshold, Scenario.Count, plainCreation);
        plainSelected = Select(plainCreation, plainShareLength);
        vssRecovered = new byte[secret.Length];
        plainRecovered = new byte[secret.Length];
        VssCreate(); ShamirCreate();
        if (!VssVerifyOne()) throw new InvalidOperationException("Share verification failed.");
        VssVerifiedReconstruct(); ShamirReconstruct();
        if (!secret.AsSpan().SequenceEqual(vssRecovered) || !secret.AsSpan().SequenceEqual(plainRecovered))
            throw new InvalidOperationException("Recovery failed.");
    }

    private byte[] Select(byte[] shares, int length)
    {
        byte[] selected = new byte[length * Scenario.Threshold];
        for (int i = 0; i < Scenario.Threshold; i++)
        {
            int index = (i & 1) == 0 ? Scenario.Count - 1 - i / 2 : i / 2;
            shares.AsSpan(index * length, length).CopyTo(selected.AsSpan(i * length));
        }
        return selected;
    }

    [Benchmark]
    public int ShamirCreate() => ShamirSharedSecret.CreateShares(secret, Scenario.Threshold, Scenario.Count, plainCreation);
    [Benchmark]
    public int VssCreate() => PedersenVss.CreateShares(secret, Scenario.Threshold, Scenario.Count, vssCreation, commitmentCreation);
    [Benchmark]
    public bool VssVerifyOne() => PedersenVss.VerifyShare(vssSelected.AsSpan(0, vssShareLength), commitments);
    [Benchmark]
    public int ShamirReconstruct() => ShamirSharedSecret.Reconstruct(plainSelected, plainShareLength, Scenario.Threshold, plainRecovered);
    [Benchmark]
    public int VssVerifiedReconstruct() => PedersenVss.Reconstruct(vssSelected, commitments, vssRecovered);

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (byte[] buffer in new[] { secret, vssCreation, commitmentCreation, vssSelected, commitments, vssRecovered, plainCreation, plainSelected, plainRecovered })
            CryptographicOperations.ZeroMemory(buffer);
    }
}
