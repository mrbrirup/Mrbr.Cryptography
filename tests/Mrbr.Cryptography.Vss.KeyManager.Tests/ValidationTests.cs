using Mrbr.Cryptography.Integration;
using Sharing = Mrbr.Cryptography.Vss.KeyManager.AuthenticatedKeySharing;

namespace Mrbr.Cryptography.Vss.KeyManager.Tests;

public sealed class ValidationTests
{
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(15)] [InlineData(17)] [InlineData(31)] [InlineData(33)]
    public void AesKeyLengthIsExplicit(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.GetPacketLength(length, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.GetCreationWorkspaceLength(length, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.WriteContext(0, length, [], new byte[100]));
    }

    [Fact]
    public void SizingRejectsOverflowAndInvalidThresholds()
    {
        Assert.Equal(48, Sharing.GetContextLength(0)); Assert.Equal(316, Sharing.GetCreationWorkspaceLength(32, 3, 0)); // 32 + 236 + 48.
        Assert.Equal(400, Sharing.GetPacketLength(32, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.GetContextLength(-1));
        Assert.Throws<OverflowException>(() => Sharing.GetContextLength(int.MaxValue));
        Assert.Throws<OverflowException>(() => Sharing.GetCreationWorkspaceLength(32, 3, int.MaxValue - 48));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.GetPacketLength(32, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.GetPacketLength(32, 256, 0));
    }

    [Fact]
    public void CallerErrorsPrecedeReplayAndDoNotMutateAnyBuffers()
    {
        using var f = new RecoveryFixture();
        byte[] shares = RecoveryFixture.Filled(f.Shares.Length), packet = RecoveryFixture.Filled(f.Packet.Length), scratch = RecoveryFixture.Filled(f.CreationWorkspace.Length);
        Assert.Throws<ArgumentNullException>(() => Sharing.CreateKeyShares(null!, 255, 32, 3, 5, f.Application, 0, f.Seed, shares, packet, scratch));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.CreateKeyShares(f.Environment.Service, 255, 32, 1, 5, f.Application, 0, f.Seed, shares, packet, scratch));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sharing.CreateKeyShares(f.Environment.Service, 255, 32, 6, 5, f.Application, 0, f.Seed, shares, packet, scratch));
        Assert.Throws<ArgumentException>(() => Sharing.CreateKeyShares(f.Environment.Service, 255, 32, 3, 5, f.Application, 0, new byte[31], shares, packet, scratch));
        Assert.Throws<ArgumentException>(() => Sharing.CreateKeyShares(f.Environment.Service, 255, 32, 3, 5, f.Application, 0, f.Seed, shares.AsSpan(0, 1), packet, scratch));
        Assert.Throws<ArgumentException>(() => Sharing.CreateKeyShares(f.Environment.Service, 255, 32, 3, 5, f.Application, 0, f.Seed, shares, packet.AsSpan(0, 1), scratch));
        Assert.Throws<ArgumentException>(() => Sharing.CreateKeyShares(f.Environment.Service, 255, 32, 3, 5, f.Application, 0, f.Seed, shares, packet, scratch.AsSpan(0, 1)));
        RecoveryTests.AssertGuard(shares, 0); RecoveryTests.AssertGuard(packet, 0); RecoveryTests.AssertGuard(scratch, 0);
        byte[] key = RecoveryFixture.Filled(39), recovery = f.RecoveryWorkspace;
        Assert.Throws<ArgumentException>(() => Sharing.TryReconstructKey(f.Select(1, 2, 3), f.PacketBytes, f.Handle, 32, f.Application, RecoveryFixture.Generation, new byte[31], key, recovery, out _));
        Assert.Throws<ArgumentException>(() => Sharing.TryReconstructKey(f.Select(1, 2, 3), f.PacketBytes, f.Handle, 32, f.Application, RecoveryFixture.Generation, f.PublicKey, key.AsSpan(0, 31), recovery, out _));
        Assert.Throws<ArgumentException>(() => Sharing.TryReconstructKey(f.Select(1, 2, 3), f.PacketBytes, f.Handle, 32, f.Application, RecoveryFixture.Generation, f.PublicKey, key, recovery.AsSpan(0, 1), out _));
        RecoveryTests.AssertGuard(key, 0); RecoveryTests.AssertGuard(recovery, 0);
    }

    [Fact]
    public void ReplayFailureWipesUsedScratchAndLeavesBothOutputsUnchanged()
    {
        using var f = new RecoveryFixture();
        byte[] shares = RecoveryFixture.Filled(f.Shares.Length), packet = RecoveryFixture.Filled(f.Packet.Length), scratch = RecoveryFixture.Filled(f.CreationWorkspace.Length);
        Assert.Throws<KeyNotFoundException>(() => Sharing.CreateKeyShares(f.Environment.Service, 255, 32, 3, 5, f.Application, 0, f.Seed, shares, packet, scratch));
        RecoveryTests.AssertGuard(shares, 0); RecoveryTests.AssertGuard(packet, 0);
        Assert.True(scratch.AsSpan(0, scratch.Length - 7).IndexOfAnyExcept((byte)0) < 0); RecoveryTests.AssertGuard(scratch, scratch.Length - 7);
        IntegrationKeyEnvironment.ResetRegistry();
        Assert.Throws<InvalidOperationException>(() => Sharing.CreateKeyShares(f.Environment.Service, f.Handle, 32, 3, 5, f.Application, 0, f.Seed, shares, packet, scratch));
        RecoveryTests.AssertGuard(shares, 0); RecoveryTests.AssertGuard(packet, 0);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CreationOutputsAndWorkspaceMustBeMutuallyDisjoint(int pair)
    {
        using var f = new RecoveryFixture();
        byte[] shared = RecoveryFixture.Filled(4096), other = RecoveryFixture.Filled(4096);
        Assert.Throws<ArgumentException>(() => Sharing.CreateKeyShares(f.Environment.Service, f.Handle, 32, 3, 5, f.Application, 0, f.Seed,
            pair is 0 or 1 ? shared : other, pair is 0 or 2 ? shared : other, pair is 1 or 2 ? shared : other));
        RecoveryTests.AssertGuard(shared, 0); RecoveryTests.AssertGuard(other, 0);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public void CreationInputsCannotAliasUnusedOutputOrWorkspaceSuffixes(bool seedInput, int target)
    {
        using var f = new RecoveryFixture();
        byte[] shares = RecoveryFixture.Filled(f.Shares.Length + 64), packet = RecoveryFixture.Filled(f.Packet.Length + 64), scratch = RecoveryFixture.Filled(f.CreationWorkspace.Length + 64);
        byte[] input = seedInput ? f.Seed : f.Application;
        byte[] alias = target == 0 ? shares : target == 1 ? packet : scratch;
        int offset = alias.Length - input.Length;
        input.CopyTo(alias, offset);
        byte[] savedShares = shares.ToArray(), savedPacket = packet.ToArray(), savedScratch = scratch.ToArray();
        Assert.Throws<ArgumentException>(() => Sharing.CreateKeyShares(f.Environment.Service, f.Handle, 32, 3, 5,
            seedInput ? f.Application : alias.AsSpan(offset, input.Length), 0, seedInput ? alias.AsSpan(offset, input.Length) : f.Seed, shares, packet, scratch));
        Assert.Equal(savedShares, shares); Assert.Equal(savedPacket, packet); Assert.Equal(savedScratch, scratch);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 1)] [InlineData(0, 2)] [InlineData(0, 3)]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 2)] [InlineData(1, 3)]
    public void RecoveryDestinationAndWorkspaceMustBeDisjointFromEveryInput(int target, int input)
    {
        using var f = new RecoveryFixture();
        byte[] selected = f.Select(1, 2, 3);
        byte[] data = input switch { 0 => selected, 1 => f.PacketBytes.ToArray(), 2 => f.Application, _ => f.PublicKey };
        byte[] alias = RecoveryFixture.Filled(128 + data.Length), key = RecoveryFixture.Filled(39), workspace = RecoveryFixture.Filled(f.RecoveryWorkspace.Length);
        data.CopyTo(alias, 128); byte[] before = alias.ToArray();
        Assert.Throws<ArgumentException>(() => Sharing.TryReconstructKey(
            input == 0 ? alias.AsSpan(128, data.Length) : selected,
            input == 1 ? alias.AsSpan(128, data.Length) : f.PacketBytes, f.Handle, 32,
            input == 2 ? alias.AsSpan(128, data.Length) : f.Application, RecoveryFixture.Generation,
            input == 3 ? alias.AsSpan(128, data.Length) : f.PublicKey,
            target == 0 ? alias : key, target == 1 ? alias : workspace, out _));
        Assert.Equal(before, alias); RecoveryTests.AssertGuard(key, 0); RecoveryTests.AssertGuard(workspace, 0);
    }

    [Fact]
    public void DisjointSlicesOfOneBufferWorkAndContextOverlapIsRejected()
    {
        using var f = new RecoveryFixture();
        int s = f.Shares.Length - 7, p = f.Packet.Length - 7, w = f.CreationWorkspace.Length - 7, c = f.RecoveryWorkspace.Length - 7;
        byte[] buffer = new byte[s + p + w + 32 + c];
        Sharing.CreateKeyShares(f.Environment.Service, f.Handle, 32, 3, 5, f.Application, RecoveryFixture.Generation, f.Seed,
            buffer.AsSpan(0, s), buffer.AsSpan(s, p), buffer.AsSpan(s + p, w));
        Assert.True(Sharing.TryReconstructKey(buffer.AsSpan(0, 3 * f.ShareLength), buffer.AsSpan(s, p), f.Handle, 32, f.Application,
            RecoveryFixture.Generation, f.PublicKey, buffer.AsSpan(s + p + w, 32), buffer.AsSpan(s + p + w + 32, c), out _));
        Assert.True(buffer.AsSpan(s + p + w, 32).SequenceEqual(f.ExpectedKey));
        Assert.Throws<ArgumentException>(() => Sharing.WriteContext(0, 32, buffer.AsSpan(100, 10), buffer));
        Assert.Throws<ArgumentException>(() => Sharing.TryReconstructKey(f.Select(1, 2, 3), f.PacketBytes, f.Handle, 32, f.Application,
            RecoveryFixture.Generation, f.PublicKey, buffer, buffer, out _));
    }
}
