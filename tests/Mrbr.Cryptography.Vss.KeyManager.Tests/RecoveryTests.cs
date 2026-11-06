using System.Buffers.Binary;
using System.Security.Cryptography;
using Mrbr.Cryptography.Integration;
using Mrbr.Cryptography.Vss.Authentication;
using Mrbr.Service.EncryptionManager.Services;
using Sharing = Mrbr.Cryptography.Vss.KeyManager.AuthenticatedKeySharing;

namespace Mrbr.Cryptography.Vss.KeyManager.Tests;

public sealed class RecoveryTests
{
    [Theory]
    [InlineData(0, 16, 0)] [InlineData(0, 24, 248)] [InlineData(0, 32, 249)]
    [InlineData(1, 16, 64)] [InlineData(1, 24, 4096)] [InlineData(1, 32, 0)]
    public void AllFourStepsRecoverKnownPlaintextWithoutTheOriginalSource(byte source, int keyLength, int aadLength)
    {
        using var f = new RecoveryFixture(source, keyLength, aadLength: aadLength);
        byte[] selected = f.Select(5, 2, 4), key = RecoveryFixture.Filled(keyLength + 7);
        Assert.True(f.CreationWorkspace.AsSpan(0, f.CreationWorkspace.Length - 7).IndexOfAnyExcept((byte)0) < 0);
        AssertGuard(f.CreationWorkspace, f.CreationWorkspace.Length - 7); AssertGuard(f.Packet, f.Packet.Length - 7); AssertGuard(f.Shares, f.Shares.Length - 7);
        IntegrationKeyEnvironment.ResetRegistry();
        Assert.Throws<InvalidOperationException>(() => f.Environment.Service.GetKey(f.Handle, new byte[keyLength]));
        try
        {
            Assert.True(f.Recover(selected, key, out int written)); Assert.Equal(keyLength, written);
            Assert.True(key.AsSpan(0, keyLength).SequenceEqual(f.ExpectedKey)); AssertGuard(key, keyLength);
            AssertClearedWorkspace(f);
            using var decryptor = new AesGcmKeyDecryptor(key.AsSpan(0, keyLength));
            CryptographicOperations.ZeroMemory(key.AsSpan(0, keyLength));
            byte[] output = RecoveryFixture.Filled(f.Plaintext.Length + 7), aadWorkspace = RecoveryFixture.Filled(aadLength + 8 + 7);
            Assert.Equal(f.Plaintext.Length, decryptor.Decrypt(f.Handle, f.Cipher, output, f.Aad, aadWorkspace));
            Assert.True(output.AsSpan(0, f.Plaintext.Length).SequenceEqual(f.Plaintext)); AssertGuard(output, f.Plaintext.Length);
            Assert.True(aadWorkspace.AsSpan(0, aadLength + 8).IndexOfAnyExcept((byte)0) < 0); AssertGuard(aadWorkspace, aadLength + 8);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(selected); }
    }

    [Fact]
    public void EveryThreeOfFiveSubsetRecoversAndDecrypts()
    {
        using var f = new RecoveryFixture(plaintextLength: 33);
        IntegrationKeyEnvironment.ResetRegistry();
        Span<byte> key = stackalloc byte[32];
        try
        {
            for (int a = 1; a <= 3; a++) for (int b = a + 1; b <= 4; b++) for (int c = b + 1; c <= 5; c++)
            {
                Assert.True(f.Recover(f.Select(c, a, b), key, out _));
                using var decryptor = new AesGcmKeyDecryptor(key);
                CryptographicOperations.ZeroMemory(key);
                byte[] output = new byte[f.Plaintext.Length];
                decryptor.Decrypt(f.Handle, f.Cipher, output, f.Aad);
                Assert.Equal(f.Plaintext, output);
            }
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    [Theory]
    [InlineData(2, false)] [InlineData(3, true)] [InlineData(5, false)] [InlineData(9, true)]
    public void DenseAndSparseQuorumsAndSurplusSharesRecover(int threshold, bool sparse)
    {
        int count = sparse ? 255 : Math.Max(threshold, 5);
        using var f = new RecoveryFixture(threshold: threshold, count: count, plaintextLength: 0);
        int[] coordinates = Enumerable.Range(0, threshold).Select(i => sparse ? (254 + 73 * i) % 255 + 1 : threshold - i).ToArray();
        byte[] key = new byte[32];
        Assert.True(f.Recover(f.Select(coordinates), key, out _)); Assert.Equal(f.ExpectedKey, key);
        if (!sparse) { Assert.True(f.Recover(f.Shares.AsSpan(0, count * f.ShareLength), key, out _)); Assert.Equal(f.ExpectedKey, key); }
        using var decryptor = new AesGcmKeyDecryptor(key); CryptographicOperations.ZeroMemory(key);
        Assert.Equal(0, decryptor.Decrypt(f.Handle, f.Cipher, [], f.Aad));
    }

    [Theory]
    [InlineData("signer")] [InlineData("application")] [InlineData("generation")]
    [InlineData("handle")] [InlineData("length")] [InlineData("signature")]
    [InlineData("packet")] [InlineData("share")] [InlineData("duplicate")]
    [InlineData("insufficient")] [InlineData("extra")]
    public void AuthenticationAndVssFailuresLeaveKeyAndPlaintextUnpublished(string change)
    {
        using var f = new RecoveryFixture(plaintextLength: 33);
        byte[] shares = f.Select(5, 2, 4), application = f.Application.ToArray(), publicKey = f.PublicKey.ToArray();
        ulong handle = f.Handle, generation = RecoveryFixture.Generation; int keyLength = 32;
        switch (change)
        {
            case "signer": AuthenticatedCommitmentPacket.DerivePublicKey(new byte[32], publicKey); break;
            case "application": application[0] ^= 1; break;
            case "generation": generation--; break;
            case "handle": handle ^= 1; break;
            case "length": keyLength = 16; break;
            case "signature": f.PacketBytes[^1] ^= 1; break;
            case "packet": f.PacketBytes[52 + Sharing.ContextHeaderLength + f.Application.Length + 12] ^= 1; break;
            case "share": shares[33] ^= 1; break;
            case "duplicate": shares.AsSpan(0, f.ShareLength).CopyTo(shares.AsSpan(f.ShareLength)); break;
            case "insufficient": shares = f.Select(5, 2); break;
            case "extra": shares = f.Select(1, 2, 3, 4, 5); shares[4 * f.ShareLength + 33] ^= 1; break;
        }
        byte[] key = RecoveryFixture.Filled(39), output = RecoveryFixture.Filled(40);
        bool result = Sharing.TryReconstructKey(shares, f.PacketBytes, handle, keyLength, application, generation, publicKey, key, f.RecoveryWorkspace, out int written);
        if (result) { using var decryptor = new AesGcmKeyDecryptor(key.AsSpan(0, keyLength)); decryptor.Decrypt(handle, f.Cipher, output, f.Aad); }
        Assert.False(result); Assert.Equal(0, written); AssertGuard(key, 0); AssertGuard(output, 0); AssertClearedWorkspace(f);
    }

    [Theory]
    [InlineData("handle")] [InlineData("nonce")] [InlineData("tag")] [InlineData("cipher")] [InlineData("aad")]
    public void CipherAuthenticationIsStillRequiredAfterSuccessfulKeyRecovery(string change)
    {
        using var f = new RecoveryFixture(plaintextLength: 33);
        byte[] key = new byte[32]; Assert.True(f.Recover(f.Select(5, 2, 4), key, out _));
        using var decryptor = new AesGcmKeyDecryptor(key); CryptographicOperations.ZeroMemory(key);
        ulong handle = f.Handle;
        switch (change)
        {
            case "handle": handle ^= 1; break;
            case "nonce": f.Cipher[0] ^= 1; break;
            case "tag": f.Cipher[12] ^= 1; break;
            case "cipher": f.Cipher[28] ^= 1; break;
            case "aad": f.Aad[0] ^= 1; break;
        }
        byte[] output = RecoveryFixture.Filled(40), scratch = RecoveryFixture.Filled(18);
        Assert.Throws<AuthenticationTagMismatchException>(() => decryptor.Decrypt(handle, f.Cipher, output, f.Aad, scratch));
        Assert.True(output.AsSpan(0, 33).IndexOfAnyExcept((byte)0) < 0); AssertGuard(output, 33);
        Assert.True(scratch.AsSpan(0, 11).IndexOfAnyExcept((byte)0) < 0); AssertGuard(scratch, 11);
    }

    [Fact]
    public void SignedInnerKeyLengthMustMatchBeforeAnyOutputWrite()
    {
        using var f = new RecoveryFixture();
        byte[] inner = new byte[PedersenVss.GetCommitmentsLength(16, 3)], shares = new byte[PedersenVss.GetSharesLength(16, 3)];
        PedersenVss.CreateShares(new byte[16], 3, 3, shares, inner);
        byte[] context = new byte[Sharing.GetContextLength(f.Application.Length)];
        Sharing.WriteContext(f.Handle, 32, f.Application, context); // Valid signer claims 32 but inner commits 16.
        byte[] packet = new byte[AuthenticatedCommitmentPacket.GetPacketLength(inner.Length, context.Length)];
        AuthenticatedCommitmentPacket.Create(inner, context, RecoveryFixture.Generation, f.Seed, packet);
        Assert.True(AuthenticatedCommitmentPacket.TryVerify(packet, context, RecoveryFixture.Generation, f.PublicKey, out _));
        byte[] output = RecoveryFixture.Filled(39);
        Assert.False(Sharing.TryReconstructKey(shares, packet, f.Handle, 32, f.Application, RecoveryFixture.Generation, f.PublicKey, output, f.RecoveryWorkspace, out int written));
        Assert.Equal(0, written); AssertGuard(output, 0); AssertClearedWorkspace(f);
    }

    [Fact]
    public void CanonicalContextAndEnvelopeInteroperateWithExistingAuthenticationApi()
    {
        using var f = new RecoveryFixture();
        byte[] context = RecoveryFixture.Filled(Sharing.GetContextLength(f.Application.Length) + 7);
        int length = Sharing.WriteContext(f.Handle, 32, f.Application, context);
        Assert.True(context.AsSpan(0, 32).SequenceEqual("Mrbr.Cryptography.Vss.KeyManager"u8));
        Assert.Equal(f.Handle, BinaryPrimitives.ReadUInt64LittleEndian(context.AsSpan(32)));
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(context.AsSpan(40)));
        Assert.Equal((uint)f.Application.Length, BinaryPrimitives.ReadUInt32LittleEndian(context.AsSpan(44)));
        Assert.True(context.AsSpan(48, f.Application.Length).SequenceEqual(f.Application)); AssertGuard(context, length);
        Assert.True(AuthenticatedCommitmentPacket.TryVerify(f.PacketBytes, context.AsSpan(0, length), RecoveryFixture.Generation, f.PublicKey, out var commitments));
        byte[] output = new byte[32];
        Assert.Equal(32, PedersenVss.Reconstruct(f.Select(5, 2, 4), commitments, output)); Assert.Equal(f.ExpectedKey, output);
        CryptographicOperations.ZeroMemory(output);
    }

    internal static void AssertGuard(byte[] bytes, int prefix) => Assert.True(bytes.AsSpan(prefix).IndexOfAnyExcept((byte)0xA5) < 0);
    internal static void AssertClearedWorkspace(RecoveryFixture f)
    {
        Assert.True(f.RecoveryWorkspace.AsSpan(0, f.RecoveryWorkspace.Length - 7).IndexOfAnyExcept((byte)0) < 0);
        AssertGuard(f.RecoveryWorkspace, f.RecoveryWorkspace.Length - 7);
    }
}
