using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using Mrbr.Cryptography.Integration;
using Mrbr.Cryptography.KeyManager;
using Mrbr.Service.EncryptionManager.Enums.Algorithms;
using Mrbr.Service.EncryptionManager.Services;

namespace Mrbr.Cryptography.IntegrationBenchmarks;

// Initialization/recovery is deliberately included in the methods that name it.
// ReusedRawKeyDecrypt measures a context whose key was already recovered/imported.
[MemoryDiagnoser, Config(typeof(IntegrationConfig))]
public class DecryptionBenchmarks
{
    [Params(16, 24, 32)] public int KeyLength { get; set; }
    private IntegrationKeyEnvironment environment = null!;
    private CryptographicService service = null!;
    private AesGcmKeyDecryptor decryptor = null!;
    private EncryptionOptions options = null!;
    private byte[] key = null!, cipher = null!, plaintext = null!, recovered = null!, shares = null!, handleShares = null!;
    private ulong handle;

    [GlobalSetup]
    public void Setup()
    {
        environment = new();
        service = new(environment.Service);
        options = new() { Algorithm = KeyLength switch { 16 => SymmetricEncryptionAlgorithms.AES128, 24 => SymmetricEncryptionAlgorithms.AES192, _ => SymmetricEncryptionAlgorithms.AES256 } };
        plaintext = RandomNumberGenerator.GetBytes(1024);
        cipher = new byte[1052];
        recovered = new byte[1024];
        key = new byte[KeyLength];
        service.Encrypt(0, plaintext, cipher, out handle, options);
        environment.Service.GetKey(handle, key);
        shares = new byte[(KeyLength + 1) * 5];
        ShamirKeySharing.CreateKeyShares(environment.Service, handle, KeyLength, 3, 5, shares);
        handleShares = new byte[45];
        ShamirKeySharing.CreateHandleShares(handle, 3, 5, handleShares);
        decryptor = new(key);
        foreach (Func<int> operation in new Func<int>[] { ExistingHandleDecrypt, ReusedRawKeyDecrypt, InitializeRawKeyAndDecrypt, RecoverHandleAndDecrypt, RecoverKeyInitializeAndDecrypt })
        {
            operation();
            if (!plaintext.AsSpan().SequenceEqual(recovered)) throw new InvalidOperationException("Decryption failed.");
        }
    }

    [Benchmark(Baseline = true)]
    public int ExistingHandleDecrypt() => service.Decrypt(handle, cipher, recovered, options);
    [Benchmark]
    public int ReusedRawKeyDecrypt() => decryptor.Decrypt(handle, cipher, recovered);
    [Benchmark]
    public int InitializeRawKeyAndDecrypt()
    {
        using var context = new AesGcmKeyDecryptor(key);
        return context.Decrypt(handle, cipher, recovered);
    }
    [Benchmark]
    public int RecoverHandleAndDecrypt() => service.Decrypt(ShamirKeySharing.ReconstructHandle(handleShares.AsSpan(0, 27), 3), cipher, recovered, options);
    [Benchmark]
    public int RecoverKeyInitializeAndDecrypt()
    {
        Span<byte> recoveredKey = stackalloc byte[KeyLength];
        try
        {
            ShamirKeySharing.ReconstructKey(shares.AsSpan(0, (KeyLength + 1) * 3), KeyLength, 3, recoveredKey);
            using var context = new AesGcmKeyDecryptor(recoveredKey);
            return context.Decrypt(handle, cipher, recovered);
        }
        finally { CryptographicOperations.ZeroMemory(recoveredKey); }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        decryptor.Dispose();
        foreach (byte[] buffer in new[] { key, cipher, plaintext, recovered, shares, handleShares }) CryptographicOperations.ZeroMemory(buffer);
        environment.Dispose();
    }
}
