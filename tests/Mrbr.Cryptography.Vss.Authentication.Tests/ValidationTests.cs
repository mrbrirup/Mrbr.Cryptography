using Packet = Mrbr.Cryptography.Vss.Authentication.AuthenticatedCommitmentPacket;

namespace Mrbr.Cryptography.Vss.Authentication.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void SizingIsCheckedAndHasNoArbitraryContextLimit()
    {
        Assert.Equal(160, Packet.GetPacketLength(44, 0));
        Assert.Equal(int.MaxValue, Packet.GetPacketLength(int.MaxValue - 116, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Packet.GetPacketLength(43, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Packet.GetPacketLength(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Packet.GetPacketLength(44, -1));
        Assert.Throws<OverflowException>(() => Packet.GetPacketLength(int.MaxValue, 0));
        Assert.Throws<OverflowException>(() => Packet.GetPacketLength(44, int.MaxValue));
    }

    [Theory]
    [InlineData(0)] [InlineData(31)] [InlineData(33)] [InlineData(64)]
    public void SeedMustBeExactly32BytesAndCallerErrorsDoNotWrite(int seedLength)
    {
        var f = new PacketTests.Fixture(0, 2, 2);
        byte[] seed = new byte[seedLength], output = Enumerable.Repeat((byte)0xA5, f.Envelope.Length).ToArray();
        Assert.Throws<ArgumentException>(() => Packet.Create(f.Commitments, f.Context, PacketTests.Fixture.Generation, seed, output));
        Assert.Throws<ArgumentException>(() => Packet.DerivePublicKey(seed, output));
        Assert.True(output.AsSpan().IndexOfAnyExcept((byte)0xA5) < 0);
    }

    [Theory]
    [InlineData(0)] [InlineData(31)] [InlineData(33)] [InlineData(64)]
    public void TrustedKeyMustBeExactly32Bytes(int keyLength)
    {
        var f = new PacketTests.Fixture(0, 2, 2);
        byte[] key = new byte[keyLength], output = [0xA5];
        Assert.Throws<ArgumentException>(() => Packet.TryVerify(f.Envelope, f.Context, PacketTests.Fixture.Generation, key, out _));
        Assert.Throws<ArgumentException>(() => Packet.TryReconstruct(f.Shares, f.Envelope, f.Context, PacketTests.Fixture.Generation, key, output, out _));
        Assert.Equal(0xA5, output[0]);
    }

    [Fact]
    public void SmallDestinationsAndMalformedCreationInputsAreRejectedBeforeWrites()
    {
        var f = new PacketTests.Fixture();
        byte[] output = Enumerable.Repeat((byte)0xA5, f.Envelope.Length).ToArray();
        Assert.Throws<ArgumentException>(() => Packet.GenerateSigningSeed(output.AsSpan(0, 31)));
        Assert.Throws<ArgumentException>(() => Packet.DerivePublicKey(f.Seed, output.AsSpan(0, 31)));
        Assert.Throws<ArgumentException>(() => Packet.Create(f.Commitments, f.Context, PacketTests.Fixture.Generation, f.Seed, output.AsSpan(1)));
        Assert.Throws<ArgumentException>(() => Packet.Create(f.Commitments.AsSpan(0, 43), f.Context, PacketTests.Fixture.Generation, f.Seed, output));
        Assert.Throws<ArgumentException>(() => Packet.TryReconstruct(f.Shares, f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output.AsSpan(0, 31), out _));
        Assert.True(output.AsSpan().IndexOfAnyExcept((byte)0xA5) < 0);
        byte[] malformed = f.Commitments.ToArray(); malformed[4] = 2;
        Assert.Throws<ArgumentException>(() => Packet.Create(malformed, f.Context, PacketTests.Fixture.Generation, f.Seed, output));
    }

    [Fact]
    public void KeyGenerationAndDerivationRespectSuffixesAndDoNotMutateSeed()
    {
        byte[] seed = Enumerable.Repeat((byte)0xA5, 39).ToArray(), output = seed.ToArray();
        Packet.GenerateSigningSeed(seed);
        Assert.True(seed.AsSpan(32).IndexOfAnyExcept((byte)0xA5) < 0);
        byte[] saved = seed.ToArray();
        Packet.DerivePublicKey(seed.AsSpan(0, 32), output);
        Assert.Equal(saved, seed); Assert.True(output.AsSpan(32).IndexOfAnyExcept((byte)0xA5) < 0);
        Assert.True(output.AsSpan(0, 32).SequenceEqual(Ed25519Reference.PublicKey(seed[..32])));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CreationRejectsEveryInputOverlapIncludingUnusedDestinationSuffix(int input)
    {
        var f = new PacketTests.Fixture(0, 2, 2);
        byte[] output = Enumerable.Repeat((byte)0xA5, f.Envelope.Length + 80).ToArray();
        int offset = f.Envelope.Length + 4;
        byte[] data = input == 0 ? f.Commitments : input == 1 ? f.Context : f.Seed;
        data.CopyTo(output, offset); byte[] before = output.ToArray();
        Assert.Throws<ArgumentException>(() => Packet.Create(
            input == 0 ? output.AsSpan(offset, data.Length) : f.Commitments,
            input == 1 ? output.AsSpan(offset, data.Length) : f.Context,
            PacketTests.Fixture.Generation,
            input == 2 ? output.AsSpan(offset, data.Length) : f.Seed, output));
        Assert.Equal(before, output);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RecoveryRejectsOverlapWithEveryFullInput(int input)
    {
        var f = new PacketTests.Fixture();
        byte[] data = input switch { 0 => f.Shares, 1 => f.Envelope, 2 => f.Context, _ => f.PublicKey };
        byte[] output = Enumerable.Repeat((byte)0xA5, 40 + data.Length).ToArray();
        data.CopyTo(output, 40); byte[] before = output.ToArray();
        Assert.Throws<ArgumentException>(() => Packet.TryReconstruct(
            input == 0 ? output.AsSpan(40, data.Length) : f.Shares,
            input == 1 ? output.AsSpan(40, data.Length) : f.Envelope,
            input == 2 ? output.AsSpan(40, data.Length) : f.Context, PacketTests.Fixture.Generation,
            input == 3 ? output.AsSpan(40, data.Length) : f.PublicKey, output, out _));
        Assert.Equal(before, output);
    }

    [Fact]
    public void PublicKeyDerivationRejectsOverlapAndDisjointSlicesRemainSupported()
    {
        byte[] storage = new byte[100];
        Assert.Throws<ArgumentException>(() => Packet.DerivePublicKey(storage.AsSpan(50, 32), storage));
        Packet.DerivePublicKey(storage.AsSpan(0, 32), storage.AsSpan(32, 32));
        Assert.True(storage.AsSpan(32, 32).SequenceEqual(Ed25519Reference.PublicKey(new byte[32])));
        var f = new PacketTests.Fixture();
        byte[] combined = new byte[f.Commitments.Length + f.Envelope.Length];
        f.Commitments.CopyTo(combined, 0);
        Packet.Create(combined.AsSpan(0, f.Commitments.Length), f.Context, PacketTests.Fixture.Generation,
            f.Seed, combined.AsSpan(f.Commitments.Length));
        Assert.True(Packet.TryVerify(combined.AsSpan(f.Commitments.Length), f.Context, PacketTests.Fixture.Generation, f.PublicKey, out _));
        byte[] recovery = new byte[f.Envelope.Length + 32]; f.Envelope.CopyTo(recovery, 0);
        Assert.True(Packet.TryReconstruct(f.Shares, recovery.AsSpan(0, f.Envelope.Length), f.Context, PacketTests.Fixture.Generation,
            f.PublicKey, recovery.AsSpan(f.Envelope.Length), out _));
        Assert.True(recovery.AsSpan(f.Envelope.Length).SequenceEqual(f.Secret));
    }
}
