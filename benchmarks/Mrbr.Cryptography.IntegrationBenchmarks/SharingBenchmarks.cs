using System.Buffers.Binary;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Mrbr.Cryptography.Integration;
using Mrbr.Cryptography.KeyManager;

namespace Mrbr.Cryptography.IntegrationBenchmarks;

[MemoryDiagnoser, Config(typeof(IntegrationConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class KeySharingBenchmarks
{
    [Params(0, 1)] public byte Source { get; set; }
    [Params(16, 24, 32)] public int KeyLength { get; set; }
    private IntegrationKeyEnvironment environment = null!;
    private byte[] key = null!, shares = null!, selected = null!, recovered = null!, handleShares = null!;
    private ulong handle;

    [GlobalSetup]
    public void Setup()
    {
        environment = new();
        key = new byte[KeyLength];
        recovered = new byte[KeyLength];
        environment.Service.GenerateKeyBytes(Source, key, out handle);
        shares = new byte[(KeyLength + 1) * 5];
        ShamirKeySharing.CreateKeyShares(environment.Service, handle, KeyLength, 3, 5, shares);
        selected = new byte[(KeyLength + 1) * 3];
        foreach (var (target, source) in new[] { (0, 4), (1, 0), (2, 2) })
            shares.AsSpan(source * (KeyLength + 1), KeyLength + 1).CopyTo(selected.AsSpan(target * (KeyLength + 1)));
        handleShares = new byte[45];
        ShamirKeySharing.CreateHandleShares(handle, 3, 5, handleShares);
        RecoverKey();
        if (!key.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("Key recovery failed.");
        RecoverHandleAndReplay();
        if (!key.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("Handle recovery failed.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Create")]
    public int CoreCreate() => ShamirSharedSecret.CreateShares(key, 3, 5, shares);
    [Benchmark, BenchmarkCategory("Create")]
    public int ReplayAndCreate() => ShamirKeySharing.CreateKeyShares(environment.Service, handle, KeyLength, 3, 5, shares);
    [Benchmark(Baseline = true), BenchmarkCategory("Recover")]
    public int CoreRecover() => ShamirSharedSecret.Reconstruct(selected, KeyLength + 1, 3, recovered);
    [Benchmark, BenchmarkCategory("Recover")]
    public int RecoverKey() => ShamirKeySharing.ReconstructKey(selected, KeyLength, 3, recovered);
    [Benchmark, BenchmarkCategory("Recover")]
    public void RecoverHandleAndReplay() => environment.Service.GetKey(
        ShamirKeySharing.ReconstructHandle(handleShares.AsSpan(0, 27), 3), recovered);

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (byte[] buffer in new[] { key, shares, selected, recovered, handleShares }) CryptographicOperations.ZeroMemory(buffer);
        environment.Dispose();
    }
}

[MemoryDiagnoser, Config(typeof(IntegrationConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class HandleSharingBenchmarks
{
    private const ulong Handle = 0xFEDCBA9876543210;
    private readonly byte[] encoded = new byte[8], shares = new byte[45], recovered = new byte[8];

    [GlobalSetup]
    public void Setup()
    {
        BinaryPrimitives.WriteUInt64LittleEndian(encoded, Handle);
        ShamirKeySharing.CreateHandleShares(Handle, 3, 5, shares);
        if (RecoverHandle() != Handle) throw new InvalidOperationException("Handle recovery failed.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Create")]
    public int CoreCreate() => ShamirSharedSecret.CreateShares(encoded, 3, 5, shares);
    [Benchmark, BenchmarkCategory("Create")]
    public int CreateHandle() => ShamirKeySharing.CreateHandleShares(Handle, 3, 5, shares);
    [Benchmark(Baseline = true), BenchmarkCategory("Recover")]
    public int CoreRecover() => ShamirSharedSecret.Reconstruct(shares.AsSpan(0, 27), 9, 3, recovered);
    [Benchmark, BenchmarkCategory("Recover")]
    public ulong RecoverHandle() => ShamirKeySharing.ReconstructHandle(shares.AsSpan(0, 27), 3);

    [GlobalCleanup]
    public void Cleanup()
    {
        CryptographicOperations.ZeroMemory(encoded);
        CryptographicOperations.ZeroMemory(shares);
        CryptographicOperations.ZeroMemory(recovered);
    }
}
