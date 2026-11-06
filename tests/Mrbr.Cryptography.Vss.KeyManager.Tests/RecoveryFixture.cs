using System.Security.Cryptography;
using Mrbr.Cryptography.Integration;
using Mrbr.Cryptography.Vss.Authentication;
using Mrbr.Service.EncryptionManager.Enums.Algorithms;
using Mrbr.Service.EncryptionManager.Services;
using Sharing = Mrbr.Cryptography.Vss.KeyManager.AuthenticatedKeySharing;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mrbr.Cryptography.Vss.KeyManager.Tests;

internal sealed class RecoveryFixture : IDisposable
{
    internal readonly IntegrationKeyEnvironment Environment = new();
    internal readonly int KeyLength, Threshold, Count, ShareLength;
    internal readonly ulong Handle;
    internal const ulong Generation = 0xFEDCBA9876543210;
    internal byte[] Application = "Mrbr.Tests.AesRecovery/key-42"u8.ToArray();
    internal byte[] Seed = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    internal byte[] PublicKey = new byte[32];
    internal byte[] Shares, Packet, CreationWorkspace, RecoveryWorkspace, ExpectedKey, Plaintext, Cipher, Aad;

    internal RecoveryFixture(byte source = 0, int keyLength = 32, int threshold = 3, int count = 5, int aadLength = 3, int plaintextLength = 1025)
    {
        KeyLength = keyLength; Threshold = threshold; Count = count;
        Plaintext = Enumerable.Range(0, plaintextLength).Select(i => (byte)((i * 37 + 11) & 255)).ToArray();
        Aad = Enumerable.Range(0, aadLength).Select(i => (byte)(i & 255)).ToArray();
        Cipher = new byte[Plaintext.Length + AesGcmKeyDecryptor.CipherOverhead];
        new CryptographicService(Environment.Service).Encrypt(source, Plaintext, Cipher, out Handle,
            new EncryptionOptions { Algorithm = Algorithm(keyLength), AssociatedData = Aad });
        ExpectedKey = new byte[keyLength]; Environment.Service.GetKey(Handle, ExpectedKey);
        ShareLength = PedersenVss.GetShareLength(keyLength);
        Shares = Filled(PedersenVss.GetSharesLength(keyLength, count) + 7);
        Packet = Filled(Sharing.GetPacketLength(keyLength, threshold, Application.Length) + 7);
        CreationWorkspace = Filled(Sharing.GetCreationWorkspaceLength(keyLength, threshold, Application.Length) + 7);
        RecoveryWorkspace = Filled(Sharing.GetContextLength(Application.Length) + 7);
        AuthenticatedCommitmentPacket.DerivePublicKey(Seed, PublicKey);
        Sharing.CreateKeyShares(Environment.Service, Handle, keyLength, threshold, count, Application, Generation, Seed, Shares, Packet, CreationWorkspace);
    }

    internal Span<byte> PacketBytes => Packet.AsSpan(0, Packet.Length - 7);
    internal byte[] Select(params int[] coordinates)
    {
        byte[] selected = new byte[ShareLength * coordinates.Length];
        for (int i = 0; i < coordinates.Length; i++) Shares.AsSpan((coordinates[i] - 1) * ShareLength, ShareLength).CopyTo(selected.AsSpan(i * ShareLength));
        return selected;
    }
    internal bool Recover(ReadOnlySpan<byte> shares, Span<byte> key, out int written) => Sharing.TryReconstructKey(
        shares, PacketBytes, Handle, KeyLength, Application, Generation, PublicKey, key, RecoveryWorkspace, out written);
    internal static byte[] Filled(int length) => Enumerable.Repeat((byte)0xA5, length).ToArray();
    internal static SymmetricEncryptionAlgorithms Algorithm(int length) => length switch
    {
        16 => SymmetricEncryptionAlgorithms.AES128,
        24 => SymmetricEncryptionAlgorithms.AES192,
        32 => SymmetricEncryptionAlgorithms.AES256,
        _ => throw new ArgumentOutOfRangeException(nameof(length))
    };
    public void Dispose()
    {
        foreach (byte[] buffer in new[] { Seed, ExpectedKey, Shares, CreationWorkspace, RecoveryWorkspace, Plaintext }) CryptographicOperations.ZeroMemory(buffer);
        Environment.Dispose();
    }
}
