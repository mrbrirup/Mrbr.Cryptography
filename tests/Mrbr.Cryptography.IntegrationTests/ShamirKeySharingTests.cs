using System.Buffers.Binary;
using Mrbr.Cryptography.Integration;
using Mrbr.Cryptography.KeyManager;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mrbr.Cryptography.IntegrationTests;

public sealed class ShamirKeySharingTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(ulong.MaxValue)]
    [InlineData(0xFEDCBA9876543210UL)]
    [InlineData(0x8000000000000000UL)]
    public void HandleEncodingMatchesCoreLittleEndianAndRoundTrips(ulong handle)
    {
        byte[] shares = new byte[9 * 5 + 4];
        shares.AsSpan().Fill(0xA5);
        Assert.Equal(45, ShamirKeySharing.CreateHandleShares(handle, 3, 5, shares));
        byte[] sparse = [.. shares.AsSpan(36, 9), .. shares.AsSpan(0, 9), .. shares.AsSpan(18, 9)];
        Assert.Equal(handle, ShamirKeySharing.ReconstructHandle(sparse, 3));
        Span<byte> raw = stackalloc byte[8];
        ShamirSharedSecret.Reconstruct(sparse, 9, 3, raw);
        Assert.Equal(handle, BinaryPrimitives.ReadUInt64LittleEndian(raw));
        Assert.All(shares[45..], value => Assert.Equal(0xA5, value));
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(3, 5)]
    [InlineData(5, 7)]
    [InlineData(3, 255)]
    [InlineData(255, 255)]
    public void HandlesRecoverFromDenseSparseAndAllCoordinates(int threshold, int count)
    {
        const ulong handle = 0x89ABCDEF01234567;
        byte[] shares = new byte[count * 9];
        ShamirKeySharing.CreateHandleShares(handle, threshold, count, shares);
        byte[] sparse = SelectShares(shares, 9, threshold, count);
        Assert.Equal(handle, ShamirKeySharing.ReconstructHandle(shares.AsSpan(0, threshold * 9), threshold));
        Assert.Equal(handle, ShamirKeySharing.ReconstructHandle(sparse, threshold));
        Assert.Equal(handle, ShamirKeySharing.ReconstructHandle(shares, threshold));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 16)]
    [InlineData(0, 24)]
    [InlineData(0, 32)]
    [InlineData(0, 256)]
    [InlineData(0, 257)]
    [InlineData(0, 65535)]
    [InlineData(1, 16)]
    [InlineData(1, 24)]
    [InlineData(1, 32)]
    [InlineData(1, 257)]
    [InlineData(1, 65535)]
    public void ActualBlockAndMatrixKeysRoundTripWithWipedWorkspace(byte source, int keyLength)
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] expected = new byte[keyLength];
        environment.Service.GenerateKeyBytes(source, expected, out ulong handle);
        int shareLength = keyLength + 1;
        byte[] shares = new byte[shareLength * 5 + 3];
        byte[] workspace = new byte[keyLength + 3];
        Array.Fill(shares, (byte)0xA5);
        Array.Fill(workspace, (byte)0xA5);
        Assert.Equal(shareLength * 5, ShamirKeySharing.CreateKeyShares(environment.Service, handle,
            keyLength, 3, 5, shares, workspace));
        Assert.All(workspace[..keyLength], value => Assert.Equal(0, value));
        Assert.All(workspace[keyLength..], value => Assert.Equal(0xA5, value));
        Assert.All(shares[(shareLength * 5)..], value => Assert.Equal(0xA5, value));

        byte[] sparse = SelectShares(shares, shareLength, 3, 5);
        byte[] recovered = new byte[keyLength + 3];
        Array.Fill(recovered, (byte)0xA5);
        Assert.Equal(keyLength, ShamirKeySharing.ReconstructKey(sparse, keyLength, 3, recovered));
        Assert.Equal(expected, recovered[..keyLength]);
        Assert.All(recovered[keyLength..], value => Assert.Equal(0xA5, value));
        Assert.Equal(handle, RecoverHandleThenReplay(environment, handle, expected));

        if (keyLength <= ShamirKeySharing.MaximumStackKeyLength)
        {
            ShamirKeySharing.CreateKeyShares(environment.Service, handle, keyLength, 3, 5, shares);
            ShamirKeySharing.ReconstructKey(shares.AsSpan(0, shareLength * 3), keyLength, 3, recovered);
            Assert.Equal(expected, recovered[..keyLength]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void KeyMaterialCanBeRecoveredAfterOriginalRegistryIsUnavailable(byte source)
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] key = new byte[32];
        environment.Service.GenerateKey256(source, key, out ulong handle);
        byte[] shares = new byte[33 * 5];
        ShamirKeySharing.CreateKeyShares(environment.Service, handle, 32, 3, 5, shares);
        IntegrationKeyEnvironment.ResetRegistry();
        byte[] recovered = new byte[32];
        ShamirKeySharing.ReconstructKey(shares.AsSpan(0, 99), 32, 3, recovered);
        Assert.Equal(key, recovered);
        Assert.Throws<InvalidOperationException>(() => environment.Service.GetKey(handle, recovered));
    }

    [Fact]
    public void ValidationPrecedesReplayAndLeavesBuffersUnchanged()
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] destination = Enumerable.Repeat((byte)0xA5, 99).ToArray();
        byte[] workspace = Enumerable.Repeat((byte)0xA5, 36).ToArray();
        // Source 255 does not exist. Invalid arguments must be rejected before replay.
        Assert.Throws<ArgumentOutOfRangeException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 255, 32, 1, 3, destination, workspace));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 255, 32, 4, 3, destination, workspace));
        Assert.Throws<ArgumentException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 255, 32, 2, 5, destination, workspace));
        Assert.Throws<ArgumentException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 255, 32, 2, 3, destination, workspace.AsSpan(0, 31)));
        Assert.Throws<ArgumentException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 255, 32, 2, 3, destination, destination.AsSpan(0, 32)));
        Assert.Throws<ArgumentNullException>(() => ShamirKeySharing.CreateKeyShares(null!, 255, 32, 2, 3, destination, workspace));
        Assert.All(destination, value => Assert.Equal(0xA5, value));
        Assert.All(workspace, value => Assert.Equal(0xA5, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void InvalidKeyLengthsAreRejected(int length)
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] buffer = new byte[300];
        Assert.Throws<ArgumentOutOfRangeException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 0, length, 2, 3, buffer, buffer));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShamirKeySharing.ReconstructKey(buffer, length, 2, buffer));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 0, length, 2, 3, buffer));
    }

    [Fact]
    public void ReplayFailureWipesUsedWorkspaceWithoutWritingShares()
    {
        using var environment = new IntegrationKeyEnvironment();
        byte[] destination = Enumerable.Repeat((byte)0xA5, 99).ToArray();
        byte[] workspace = Enumerable.Repeat((byte)0xA5, 36).ToArray();
        Assert.Throws<KeyNotFoundException>(() => ShamirKeySharing.CreateKeyShares(environment.Service, 255, 32, 2, 3, destination, workspace));
        Assert.All(destination, value => Assert.Equal(0xA5, value));
        Assert.All(workspace[..32], value => Assert.Equal(0, value));
        Assert.All(workspace[32..], value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void HandleFramingAndCoordinatesUseCoreValidation()
    {
        byte[] shares = new byte[27];
        ShamirKeySharing.CreateHandleShares(123, 2, 3, shares);
        Assert.Throws<ArgumentException>(() => ShamirKeySharing.ReconstructHandle(shares.AsSpan(0, 17), 2));
        Assert.Throws<ArgumentException>(() => ShamirKeySharing.ReconstructHandle(shares.AsSpan(0, 9), 2));
        shares[9] = shares[0];
        Assert.Throws<ArgumentException>(() => ShamirKeySharing.ReconstructHandle(shares, 2));
        shares[9] = 0;
        Assert.Throws<ArgumentException>(() => ShamirKeySharing.ReconstructHandle(shares, 2));
    }

    internal static byte[] SelectShares(byte[] shares, int length, int threshold, int count)
    {
        byte[] selected = new byte[length * threshold];
        for (int i = 0; i < threshold; i++)
        {
            int index = (i & 1) == 0 ? count - 1 - i / 2 : i / 2;
            shares.AsSpan(index * length, length).CopyTo(selected.AsSpan(i * length));
        }
        return selected;
    }

    private static ulong RecoverHandleThenReplay(IntegrationKeyEnvironment environment, ulong handle, byte[] expected)
    {
        byte[] shares = new byte[45];
        ShamirKeySharing.CreateHandleShares(handle, 3, 5, shares);
        ulong recovered = ShamirKeySharing.ReconstructHandle(shares.AsSpan(0, 27), 3);
        byte[] key = new byte[expected.Length];
        environment.Service.GetKey(recovered, key);
        Assert.Equal(expected, key);
        return recovered;
    }
}
