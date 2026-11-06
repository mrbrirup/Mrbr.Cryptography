using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;

namespace Mrbr.Cryptography.ComparisonBenchmarks;

public sealed class ComparisonConfig : ManualConfig
{
    public ComparisonConfig()
    {
        string target = Environment.GetEnvironmentVariable("ComparisonTargetFramework") ?? "net11.0";
        string frozen = Environment.GetEnvironmentVariable("ComparisonUseFrozenAssembly") ?? "false";
        AddJob(Job.Default.DontEnforcePowerPlan().WithArguments([new MsBuildArgument("/p:ComparisonTargetFramework=" + target),
            new MsBuildArgument("/p:ComparisonUseFrozenAssembly=" + frozen)]).AsDefault());
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddExporter(JsonExporter.Full);
    }
}

public sealed record ComparisonScenario(int Length, int Threshold, int Count)
{
    public override string ToString() => $"{Length}B_{Threshold}of{Count}";
}

[MemoryDiagnoser, Config(typeof(ComparisonConfig))]
public class ComparisonBenchmarks
{
    [ParamsSource(nameof(Scenarios))] public ComparisonScenario Scenario { get; set; } = null!;
    public IEnumerable<ComparisonScenario> Scenarios =>
    [new(8, 3, 5), new(16, 3, 5), new(24, 3, 5), new(32, 2, 3), new(32, 3, 5), new(32, 5, 7), new(32, 9, 9), new(128, 3, 5)];

    private byte[] secret = null!, creation = null!, selected = null!, recovered = null!;
    private UpstreamSubject subject = null!;
    private int shareLength;

    [GlobalSetup]
    public void Setup()
    {
        ValidateExpectedTarget();
        secret = Payload(Scenario.Length);
        shareLength = ShamirSharedSecret.GetShareLength(secret.Length);
        creation = new byte[ShamirSharedSecret.GetSharesLength(secret.Length, Scenario.Count)];
        selected = new byte[shareLength * Scenario.Threshold]; recovered = new byte[secret.Length];
        ShamirSharedSecret.CreateShares(secret, Scenario.Threshold, Scenario.Count, creation);
        for (int i = 0; i < Scenario.Threshold; i++)
            creation.AsSpan((Scenario.Count - i - 1) * shareLength, shareLength).CopyTo(selected.AsSpan(i * shareLength));
        subject = new(secret, Scenario.Threshold, Scenario.Count);
        UpstreamSubject.Describe();
        Console.WriteLine($"FIELD big={subject.BigFieldExponent}; secure={subject.SecureFieldExponent}; payload={secret.Length}; k={Scenario.Threshold}; n={Scenario.Count}");
        if (ShamirSharedSecret.Reconstruct(selected, shareLength, Scenario.Threshold, recovered) != secret.Length || !secret.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("Mrbr mismatch.");
        if (subject.ReconstructBig(recovered) != secret.Length || !secret.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("BigInteger mismatch.");
        if (subject.ReconstructSecure(recovered) != secret.Length || !secret.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("SecureBigInteger mismatch.");
    }

    internal static byte[] Payload(int length)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++) bytes[i] = (byte)((i * 31 + 19) & 255);
        bytes[0] = 0; bytes[^1] = 0; // Preserve binary zeros and exact length.
        return bytes;
    }

    internal static void ValidateExpectedTarget()
    {
        string expected = (Environment.GetEnvironmentVariable("ComparisonTargetFramework") ?? "net11.0") switch
        {
            "netstandard2.0" => ".NETStandard,Version=v2.0",
            "net10.0" => ".NETCoreApp,Version=v10.0",
            "net11.0" => ".NETCoreApp,Version=v11.0",
            _ => throw new InvalidOperationException("Unsupported comparison target.")
        };
        if (UpstreamSubject.LoadedTarget != expected) throw new InvalidOperationException($"Expected {expected}, loaded {UpstreamSubject.LoadedTarget}.");
    }

    [Benchmark]
    public int MrbrCreate()
    {
        try { return ShamirSharedSecret.CreateShares(secret, Scenario.Threshold, Scenario.Count, creation); }
        finally { CryptographicOperations.ZeroMemory(creation); }
    }
    [Benchmark]
    public int MrbrReconstruct()
    {
        try { return ShamirSharedSecret.Reconstruct(selected, shareLength, Scenario.Threshold, recovered); }
        finally { CryptographicOperations.ZeroMemory(recovered); }
    }
    [Benchmark] public int UpstreamBigCreate() => subject.CreateBig();
    [Benchmark] public int UpstreamSecureCreate() => subject.CreateSecure();
    [Benchmark]
    public int UpstreamBigReconstruct()
    {
        try { return subject.ReconstructBig(recovered); }
        finally { CryptographicOperations.ZeroMemory(recovered); }
    }
    [Benchmark]
    public int UpstreamSecureReconstruct()
    {
        try { return subject.ReconstructSecure(recovered); }
        finally { CryptographicOperations.ZeroMemory(recovered); }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        subject.Dispose();
        foreach (byte[] bytes in new[] { secret, creation, selected, recovered }) CryptographicOperations.ZeroMemory(bytes);
    }
}
