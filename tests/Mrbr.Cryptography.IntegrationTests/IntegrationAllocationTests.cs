using System.Security.Cryptography;
using Mrbr.Cryptography.Integration;
using Mrbr.Cryptography.KeyManager;
using Mrbr.Service.EncryptionManager.Services;

namespace Mrbr.Cryptography.IntegrationTests;

public sealed class IntegrationAllocationTests
{
    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(0, 24, 248)]
    [InlineData(0, 32, 4096)]
    [InlineData(1, 16, 249)]
    [InlineData(1, 24, 0)]
    [InlineData(1, 32, 64)]
    public void WarmSharingReplayAndReusableRawDecryptionAllocateZeroBytes(byte source, int keyLength, int aadLength)
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] plaintext = RandomNumberGenerator.GetBytes(1024);
        byte[] aad = new byte[aadLength];
        byte[] cipher = new byte[1052];
        new CryptographicService(environment.Service).Encrypt(source, plaintext, cipher, out ulong handle,
            new EncryptionOptions { Algorithm = RawKeyDecryptionTests.Algorithm(keyLength), AssociatedData = aad });
        byte[] shares = new byte[(keyLength + 1) * 5];
        byte[] handles = new byte[45];
        byte[] key = new byte[keyLength];
        byte[] keyWorkspace = new byte[keyLength];
        byte[] output = new byte[1024];
        byte[] aadWorkspace = new byte[aadLength + 8];
        environment.Service.GetKey(handle, key);
        using var decryptor = new AesGcmKeyDecryptor(key);
        long[] allocations = new long[7];

        for (int iteration = 0; iteration < 33; iteration++)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            ShamirKeySharing.CreateHandleShares(handle, 3, 5, handles);
            allocations[0] = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            ulong reconstructed = ShamirKeySharing.ReconstructHandle(handles.AsSpan(0, 27), 3);
            environment.Service.GetKey(reconstructed, key);
            allocations[1] = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            ShamirKeySharing.CreateKeyShares(environment.Service, handle, keyLength, 3, 5, shares);
            allocations[2] = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            ShamirKeySharing.CreateKeyShares(environment.Service, handle, keyLength, 3, 5, shares, keyWorkspace);
            allocations[3] = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            ShamirKeySharing.ReconstructKey(shares.AsSpan(0, (keyLength + 1) * 3), keyLength, 3, key);
            allocations[4] = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            decryptor.Decrypt(handle, cipher, output, aad, aadWorkspace);
            allocations[5] = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            if (aadLength <= 248) decryptor.Decrypt(handle, cipher, output, aad);
            allocations[6] = GC.GetAllocatedBytesForCurrentThread() - start;
            if (iteration > 0) Assert.All(allocations, bytes => Assert.Equal(0L, bytes));
        }
        Assert.Equal(plaintext, output);
    }
}
