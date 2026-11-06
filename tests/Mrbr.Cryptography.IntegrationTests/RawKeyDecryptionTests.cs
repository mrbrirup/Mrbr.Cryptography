using System.Security.Cryptography;
using Mrbr.Cryptography.Integration;
using Mrbr.Cryptography.KeyManager;
using Mrbr.Service.EncryptionManager.Enums.Algorithms;
using Mrbr.Service.EncryptionManager.Services;

namespace Mrbr.Cryptography.IntegrationTests;

public sealed class RawKeyDecryptionTests
{
    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(0, 24, 248)]
    [InlineData(0, 32, 249)]
    [InlineData(1, 16, 64)]
    [InlineData(1, 24, 4096)]
    [InlineData(1, 32, 0)]
    public void ReconstructedKeyDecryptsExistingCipherAfterRegistryLoss(byte source, int keyLength, int aadLength)
    {
        using var environment = new IntegrationKeyEnvironment();
        var service = new CryptographicService(environment.Service);
        byte[] plaintext = RandomNumberGenerator.GetBytes(1025);
        byte[] aad = RandomNumberGenerator.GetBytes(aadLength);
        var options = new EncryptionOptions { Algorithm = Algorithm(keyLength), AssociatedData = aad };
        byte[] cipher = new byte[plaintext.Length + 28];
        service.Encrypt(source, plaintext, cipher, out ulong handle, options);
        byte[] shares = new byte[(keyLength + 1) * 5];
        ShamirKeySharing.CreateKeyShares(environment.Service, handle, keyLength, 3, 5, shares);
        byte[] selected = ShamirKeySharingTests.SelectShares(shares, keyLength + 1, 3, 5);
        IntegrationKeyEnvironment.ResetRegistry();

        byte[] recoveredKey = new byte[keyLength];
        ShamirKeySharing.ReconstructKey(selected, keyLength, 3, recoveredKey);
        using var decryptor = new AesGcmKeyDecryptor(recoveredKey);
        CryptographicOperations.ZeroMemory(recoveredKey); // The context imported its own key.
        byte[] recovered = Enumerable.Repeat((byte)0xA5, plaintext.Length + 3).ToArray();
        byte[] workspace = Enumerable.Repeat((byte)0xA5, aadLength + 8 + 3).ToArray();
        Assert.Equal(plaintext.Length, decryptor.Decrypt(handle, cipher, recovered, aad, workspace));
        Assert.Equal(plaintext, recovered[..plaintext.Length]);
        Assert.All(recovered[plaintext.Length..], value => Assert.Equal(0xA5, value));
        Assert.All(workspace[..(aadLength + 8)], value => Assert.Equal(0, value));
        Assert.All(workspace[(aadLength + 8)..], value => Assert.Equal(0xA5, value));
        if (aadLength <= AesGcmKeyDecryptor.MaximumStackAssociatedDataLength)
            Assert.Equal(plaintext.Length, decryptor.Decrypt(handle, cipher, recovered, aad));
        else
            Assert.Throws<ArgumentException>(() => decryptor.Decrypt(handle, cipher, recovered, aad));
    }

    [Theory]
    [InlineData("key")]
    [InlineData("handle")]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("cipher")]
    [InlineData("aad")]
    [InlineData("share")]
    public void TamperingFailsAuthenticationAndClearsPlaintextAndWorkspace(string component)
    {
        using var environment = new IntegrationKeyEnvironment();
        var service = new CryptographicService(environment.Service);
        byte[] aad = [1, 2, 3];
        var options = new EncryptionOptions { AssociatedData = aad };
        byte[] cipher = new byte[60];
        service.Encrypt(0, new byte[32], cipher, out ulong handle, options);
        byte[] key = new byte[32];
        environment.Service.GetKey(handle, key);
        switch (component)
        {
            case "key": key[0] ^= 1; break;
            case "handle": handle ^= 1; break;
            case "nonce": cipher[0] ^= 1; break;
            case "tag": cipher[12] ^= 1; break;
            case "cipher": cipher[28] ^= 1; break;
            case "aad": aad[0] ^= 1; break;
            case "share":
                byte[] shares = new byte[99];
                ShamirKeySharing.CreateKeyShares(environment.Service, handle, 32, 3, 3, shares);
                shares[1] ^= 1;
                ShamirKeySharing.ReconstructKey(shares, 32, 3, key);
                break;
        }
        using var decryptor = new AesGcmKeyDecryptor(key);
        byte[] output = Enumerable.Repeat((byte)0xA5, 35).ToArray();
        byte[] workspace = Enumerable.Repeat((byte)0xA5, 14).ToArray();
        Assert.Throws<AuthenticationTagMismatchException>(() => decryptor.Decrypt(handle, cipher, output, aad, workspace));
        Assert.All(output[..32], value => Assert.Equal(0, value));
        Assert.All(output[32..], value => Assert.Equal(0xA5, value));
        Assert.All(workspace[..11], value => Assert.Equal(0, value));
        Assert.All(workspace[11..], value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void EmptyPlaintextIsAuthenticated()
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] cipher = new byte[28];
        new CryptographicService(environment.Service).Encrypt(0, [], cipher, out ulong handle);
        byte[] key = new byte[32];
        environment.Service.GetKey(handle, key);
        using var decryptor = new AesGcmKeyDecryptor(key);
        Assert.Equal(0, decryptor.Decrypt(handle, cipher, []));
        cipher[12] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() => decryptor.Decrypt(handle, cipher, []));
    }

    [Fact]
    public void InvalidSizesAndOverlapsLeaveAllBuffersUnchanged()
    {
        using var decryptor = new AesGcmKeyDecryptor(new byte[32]);
        byte[] buffer = Enumerable.Repeat((byte)0xA5, 160).ToArray();
        byte[] output = Enumerable.Repeat((byte)0xA5, 40).ToArray();
        byte[] workspace = Enumerable.Repeat((byte)0xA5, 16).ToArray();
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 27), output));
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 60), output.AsSpan(0, 31)));
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 60), output, [], workspace.AsSpan(0, 7)));
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 60), buffer.AsSpan(28, 32)));
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 60), output, output.AsSpan(0, 1)));
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 60), output, [], buffer.AsSpan(0, 8)));
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 60), output, [], output));
        Assert.Throws<ArgumentException>(() => decryptor.Decrypt(0, buffer.AsSpan(0, 60), output, workspace.AsSpan(0, 1), workspace));
        Assert.All(buffer, value => Assert.Equal(0xA5, value));
        Assert.All(output, value => Assert.Equal(0xA5, value));
        Assert.All(workspace, value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void DisjointSlicesOfSameBufferWork()
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] buffer = new byte[128];
        new CryptographicService(environment.Service).Encrypt(0, new byte[32], buffer.AsSpan(0, 60), out ulong handle);
        byte[] key = new byte[32];
        environment.Service.GetKey(handle, key);
        using var decryptor = new AesGcmKeyDecryptor(key);
        Assert.Equal(32, decryptor.Decrypt(handle, buffer.AsSpan(0, 60), buffer.AsSpan(60, 32), [], buffer.AsSpan(92, 8)));
    }

    [Fact]
    public void DisposedContextRejectsUseAndDisposalIsIdempotent()
    {
        var decryptor = new AesGcmKeyDecryptor(new byte[16]);
        decryptor.Dispose();
        decryptor.Dispose();
        Assert.Throws<ObjectDisposedException>(() => decryptor.Decrypt(0, new byte[28], []));
        Assert.Throws<ObjectDisposedException>(() => decryptor.Decrypt(0, new byte[28], [], [], new byte[8]));
    }

    [Fact]
    public void AssociatedDataSizingRejectsNegativeAndOverflow()
    {
        Assert.Equal(8, AesGcmKeyDecryptor.GetAssociatedDataWorkspaceLength(0));
        Assert.Equal(256, AesGcmKeyDecryptor.GetAssociatedDataWorkspaceLength(248));
        Assert.Throws<ArgumentOutOfRangeException>(() => AesGcmKeyDecryptor.GetAssociatedDataWorkspaceLength(-1));
        Assert.Throws<OverflowException>(() => AesGcmKeyDecryptor.GetAssociatedDataWorkspaceLength(int.MaxValue));
    }

    internal static SymmetricEncryptionAlgorithms Algorithm(int length) => length switch
    {
        16 => SymmetricEncryptionAlgorithms.AES128,
        24 => SymmetricEncryptionAlgorithms.AES192,
        32 => SymmetricEncryptionAlgorithms.AES256,
        _ => throw new ArgumentOutOfRangeException(nameof(length))
    };
}
