using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using Packet = Mrbr.Cryptography.Vss.Authentication.AuthenticatedCommitmentPacket;

namespace Mrbr.Cryptography.Vss.Authentication.Benchmarks;

public sealed class AuthenticationConfig : ManualConfig
{
    public AuthenticationConfig()
    {
        AddJob(Job.Default.DontEnforcePowerPlan().AsDefault());
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddExporter(JsonExporter.Full);
    }
}

public sealed record AuthenticationScenario(int Length, int Count, bool Sparse = false)
{
    public override string ToString() => $"{Length}B_k{Count}_m{Count}_{(Sparse ? "Sparse" : "Dense")}";
}

[MemoryDiagnoser, Config(typeof(AuthenticationConfig))]
public class AuthenticationBenchmarks
{
    [ParamsSource(nameof(Scenarios))] public AuthenticationScenario Scenario { get; set; } = null!;
    public IEnumerable<AuthenticationScenario> Scenarios =>
    [new(8, 3), new(32, 2), new(32, 3), new(32, 3, true), new(32, 5), new(32, 9), new(62, 3), new(1024, 3)];

    private byte[] seed = null!, publicKey = null!, context = null!, shares = null!, commitments = null!, envelope = null!, destination = null!;
    private int shareLength;

    [GlobalSetup]
    public void Setup()
    {
        seed = new byte[32]; publicKey = new byte[32]; context = "Mrbr.KeyRecovery/benchmark-key"u8.ToArray();
        Packet.GenerateSigningSeed(seed); Packet.DerivePublicKey(seed, publicKey);
        byte[] secret = new byte[Scenario.Length];
        for (int i = 0; i < secret.Length; i++) secret[i] = (byte)((i * 31 + 19) & 255);
        int count = Scenario.Sparse ? 255 : Math.Max(5, Scenario.Count);
        shareLength = PedersenVss.GetShareLength(secret.Length);
        byte[] all = new byte[PedersenVss.GetSharesLength(secret.Length, count)];
        commitments = new byte[PedersenVss.GetCommitmentsLength(secret.Length, Scenario.Count)];
        PedersenVss.CreateShares(secret, Scenario.Count, count, all, commitments);
        shares = new byte[shareLength * Scenario.Count];
        for (int i = 0; i < Scenario.Count; i++)
        {
            int coordinate = Scenario.Sparse ? (254 + 73 * i) % 255 + 1 : Scenario.Count - i;
            all.AsSpan((coordinate - 1) * shareLength, shareLength).CopyTo(shares.AsSpan(i * shareLength));
        }
        envelope = new byte[Packet.GetPacketLength(commitments.Length, context.Length)];
        destination = new byte[secret.Length];
        CreateEnvelope();
        if (!AuthenticatePacket() || !PlainVerifyShare() || !AuthenticatedVerifyShare()) throw new InvalidOperationException("Verification failed.");
        PlainReconstruct(); AuthenticatedReconstruct();
        if (!secret.AsSpan().SequenceEqual(destination)) throw new InvalidOperationException("Recovery failed.");
        CryptographicOperations.ZeroMemory(all); CryptographicOperations.ZeroMemory(secret);
    }

    [Benchmark] public int CreateEnvelope() => Packet.Create(commitments, context, 42, seed, envelope);
    [Benchmark] public bool AuthenticatePacket() => Packet.TryVerify(envelope, context, 42, publicKey, out _);
    [Benchmark] public bool PlainVerifyShare() => PedersenVss.VerifyShare(shares.AsSpan(0, shareLength), commitments);
    [Benchmark] public bool AuthenticatedVerifyShare() => Packet.VerifyShare(shares.AsSpan(0, shareLength), envelope, context, 42, publicKey);
    [Benchmark] public int PlainReconstruct() => PedersenVss.Reconstruct(shares, commitments, destination);
    [Benchmark] public int AuthenticatedReconstruct() => Packet.Reconstruct(shares, envelope, context, 42, publicKey, destination);

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (byte[] buffer in new[] { seed, publicKey, context, shares, commitments, envelope, destination }) CryptographicOperations.ZeroMemory(buffer);
    }
}
