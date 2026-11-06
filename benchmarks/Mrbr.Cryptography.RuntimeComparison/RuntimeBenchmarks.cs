using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using Mrbr.Cryptography.ComparisonBenchmarks;

namespace Mrbr.Cryptography.RuntimeComparison;

public sealed class RuntimeConfig : ManualConfig
{
    public RuntimeConfig()
    {
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0).WithId("Runtime10").DontEnforcePowerPlan());
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core11_0).WithId("Runtime11").DontEnforcePowerPlan());
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddExporter(JsonExporter.Full);
    }
}

[MemoryDiagnoser, Config(typeof(RuntimeConfig))]
public class RuntimeBenchmarks
{
    [Params(32, 128)] public int Length { get; set; }
    private byte[] secret = null!, recovered = null!;
    private UpstreamSubject subject = null!;
    [GlobalSetup]
    public void Setup()
    {
        secret = new byte[Length]; recovered = new byte[Length];
        for (int i = 0; i < Length; i++) secret[i] = (byte)((i * 31 + 19) & 255);
        secret[0] = 0; secret[^1] = 0;
        subject = new(secret, 3, 5); UpstreamSubject.Describe();
        if (UpstreamSubject.LoadedTarget != ".NETCoreApp,Version=v10.0") throw new InvalidOperationException("Runtime control must keep the net10 subject binary.");
        if (subject.ReconstructBig(recovered) != Length || !secret.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("BigInteger mismatch.");
        if (subject.ReconstructSecure(recovered) != Length || !secret.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("SecureBigInteger mismatch.");
    }
    [Benchmark] public int BigCreate() => subject.CreateBig();
    [Benchmark] public int SecureCreate() => subject.CreateSecure();
    [Benchmark] public int BigReconstruct()
    {
        try { return subject.ReconstructBig(recovered); }
        finally { CryptographicOperations.ZeroMemory(recovered); }
    }
    [Benchmark] public int SecureReconstruct()
    {
        try { return subject.ReconstructSecure(recovered); }
        finally { CryptographicOperations.ZeroMemory(recovered); }
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        subject.Dispose(); CryptographicOperations.ZeroMemory(secret); CryptographicOperations.ZeroMemory(recovered);
    }
}
