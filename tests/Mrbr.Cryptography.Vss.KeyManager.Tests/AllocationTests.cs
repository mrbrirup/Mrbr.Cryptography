using System.Security.Cryptography;
using Mrbr.Service.EncryptionManager.Services;
using Sharing = Mrbr.Cryptography.Vss.KeyManager.AuthenticatedKeySharing;

namespace Mrbr.Cryptography.Vss.KeyManager.Tests;

public sealed class AllocationTests
{
    [Theory]
    [InlineData(0, 16)] [InlineData(0, 24)] [InlineData(0, 32)]
    [InlineData(1, 16)] [InlineData(1, 24)] [InlineData(1, 32)]
    public void ReplaySharingSigningRecoveryAndReusedDecryptionAllocateZeroManagedBytes(byte source, int keyLength)
    {
        using var f = new RecoveryFixture(source, keyLength);
        byte[] key = new byte[keyLength], output = new byte[f.Plaintext.Length], aadWorkspace = new byte[f.Aad.Length + 8];
        byte[] badPacket = f.PacketBytes.ToArray(); badPacket[^1] ^= 1;
        Assert.True(f.Recover(f.Select(5, 2, 4), key, out _));
        using var decryptor = new AesGcmKeyDecryptor(key); CryptographicOperations.ZeroMemory(key);
        for (int pass = 0; pass < 8; pass++)
        {
            Sharing.CreateKeyShares(f.Environment.Service, f.Handle, keyLength, 3, 5, f.Application, RecoveryFixture.Generation, f.Seed, f.Shares, f.Packet, f.CreationWorkspace);
            f.Recover(f.Shares.AsSpan(0, 3 * f.ShareLength), key, out _);
            decryptor.Decrypt(f.Handle, f.Cipher, output, f.Aad, aadWorkspace);
            Sharing.TryReconstructKey(f.Shares.AsSpan(0, 3 * f.ShareLength), badPacket, f.Handle, keyLength, f.Application, RecoveryFixture.Generation, f.PublicKey, key, f.RecoveryWorkspace, out _);
        }
        _ = GC.GetAllocatedBytesForCurrentThread();
        for (int pass = 0; pass < 3; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            int created = Sharing.CreateKeyShares(f.Environment.Service, f.Handle, keyLength, 3, 5, f.Application, RecoveryFixture.Generation, f.Seed, f.Shares, f.Packet, f.CreationWorkspace);
            long createBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            bool recovered = f.Recover(f.Shares.AsSpan(0, 3 * f.ShareLength), key, out int written);
            long recoveryBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            int decrypted = decryptor.Decrypt(f.Handle, f.Cipher, output, f.Aad, aadWorkspace);
            long decryptBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            bool bad = Sharing.TryReconstructKey(f.Shares.AsSpan(0, 3 * f.ShareLength), badPacket, f.Handle, keyLength, f.Application, RecoveryFixture.Generation, f.PublicKey, key, f.RecoveryWorkspace, out _);
            long failureBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, createBytes); Assert.Equal(0L, recoveryBytes); Assert.Equal(0L, decryptBytes); Assert.Equal(0L, failureBytes);
            Assert.Equal(f.Shares.Length - 7, created); Assert.True(recovered); Assert.Equal(keyLength, written); Assert.False(bad);
            Assert.Equal(f.ExpectedKey, key); Assert.Equal(f.Plaintext.Length, decrypted); Assert.Equal(f.Plaintext, output);
        }
        CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(output);
    }
}
