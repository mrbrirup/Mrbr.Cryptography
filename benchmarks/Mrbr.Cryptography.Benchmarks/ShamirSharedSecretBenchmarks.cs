using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

namespace Mrbr.Cryptography.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(ThroughputConfig))]
public class ShamirSharedSecretBenchmarks
{
    private byte[] _secret = null!;
    private byte[] _creationDestination = null!;
    private byte[] _allShares = null!;
    private byte[] _thresholdShares = null!;
    private byte[] _recovered = null!;
    private int _shareLength;
    private int _threshold;
    private int _shareCount;

    [ParamsSource(nameof(Scenarios))]
    public SharingScenario Scenario { get; set; } = null!;

    // A curated matrix avoids an expensive Cartesian product while varying
    // payload size, threshold, total shares, and the GF(256) coordinate boundary.
    public IEnumerable<SharingScenario> Scenarios =>
    [
        new(32, 3, 5),
        new(1024, 3, 5),
        new(65536, 3, 5),
        new(1048576, 3, 5),
        new(65536, 2, 3),
        new(65536, 5, 10),
        new(65536, 10, 20),
        new(32, 3, 255),
        new(32, 255, 255),
    ];

    [GlobalSetup]
    public void Setup()
    {
        _threshold = Scenario.Threshold;
        _shareCount = Scenario.ShareCount;
        _shareLength = ShamirSharedSecret.GetShareLength(Scenario.SecretLength);
        _secret = new byte[Scenario.SecretLength];
        for (int index = 0; index < _secret.Length; index++)
        {
            _secret[index] = (byte)(index & 0xFF);
        }

        int packedLength = ShamirSharedSecret.GetSharesLength(_secret.Length, _shareCount);
        _creationDestination = new byte[packedLength];
        _allShares = new byte[packedLength];
        _thresholdShares = new byte[_threshold * _shareLength];
        _recovered = new byte[_secret.Length];

        // Reconstruction inputs are fixed outside measurement and separate from
        // the creation destination. Timed creation always obtains fresh OS entropy.
        RequireLength(ShamirSharedSecret.CreateShares(_secret, _threshold, _shareCount, _allShares), packedLength);
        for (int index = 0; index < _threshold; index++)
        {
            int share = (index & 1) == 0 ? _shareCount - 1 - (index / 2) : index / 2;
            _allShares.AsSpan(share * _shareLength, _shareLength).CopyTo(_thresholdShares.AsSpan(index * _shareLength));
        }

        // Validate all three measured paths before taking any samples.
        RequireLength(CreateShares(), packedLength);
        RequireLength(ShamirSharedSecret.Reconstruct(_creationDestination, _shareLength, _threshold, _recovered), _secret.Length);
        RequireSecret();
        RequireLength(ReconstructThreshold(), _secret.Length);
        RequireSecret();
        RequireLength(ReconstructAll(), _secret.Length);
        RequireSecret();
    }

    [Benchmark]
    public int CreateShares() => ShamirSharedSecret.CreateShares(_secret, _threshold, _shareCount, _creationDestination);

    [Benchmark]
    public int ReconstructThreshold() => ShamirSharedSecret.Reconstruct(_thresholdShares, _shareLength, _threshold, _recovered);

    [Benchmark]
    public int ReconstructAll() => ShamirSharedSecret.Reconstruct(_allShares, _shareLength, _threshold, _recovered);

    [GlobalCleanup]
    public void Cleanup()
    {
        CryptographicOperations.ZeroMemory(_secret);
        CryptographicOperations.ZeroMemory(_creationDestination);
        CryptographicOperations.ZeroMemory(_allShares);
        CryptographicOperations.ZeroMemory(_thresholdShares);
        CryptographicOperations.ZeroMemory(_recovered);
    }

    private static void RequireLength(int actual, int expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException($"Expected {expected} bytes, got {actual}.");
        }
    }

    private void RequireSecret()
    {
        if (!_secret.AsSpan().SequenceEqual(_recovered))
        {
            throw new InvalidOperationException("Benchmark setup failed to recover the secret.");
        }
    }
}
