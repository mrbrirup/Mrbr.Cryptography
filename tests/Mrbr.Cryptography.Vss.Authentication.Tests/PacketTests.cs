using System.Buffers.Binary;
using System.Security.Cryptography;
using Packet = Mrbr.Cryptography.Vss.Authentication.AuthenticatedCommitmentPacket;

namespace Mrbr.Cryptography.Vss.Authentication.Tests;

public sealed class PacketTests
{
    internal sealed class Fixture
    {
        internal byte[] Seed = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        internal byte[] PublicKey = new byte[32], Context = [0, 255, 17, 0, 4];
        internal byte[] Secret, Shares, Commitments, Envelope;
        internal const ulong Generation = 0xFEDCBA9876543210;
        internal readonly int ShareLength;
        internal Fixture(int length = 32, int threshold = 3, int count = 5, bool sparse = false)
        {
            Secret = Enumerable.Range(0, length).Select(i => (byte)((i * 31 + 11) & 255)).ToArray();
            int total = sparse ? 255 : count;
            ShareLength = PedersenVss.GetShareLength(length);
            byte[] all = new byte[PedersenVss.GetSharesLength(length, total)];
            Commitments = new byte[PedersenVss.GetCommitmentsLength(length, threshold)];
            PedersenVss.CreateShares(Secret, threshold, total, all, Commitments);
            Shares = new byte[ShareLength * count];
            for (int i = 0; i < count; i++)
            {
                int coordinate = sparse ? (254 + 73 * i) % 255 + 1 : count - i;
                all.AsSpan((coordinate - 1) * ShareLength, ShareLength).CopyTo(Shares.AsSpan(i * ShareLength));
            }
            CryptographicOperations.ZeroMemory(all);
            Packet.DerivePublicKey(Seed, PublicKey);
            Envelope = new byte[Packet.GetPacketLength(Commitments.Length, Context.Length)];
            Packet.Create(Commitments, Context, Generation, Seed, Envelope);
        }
        internal bool Verify(byte[]? envelope = null, byte[]? context = null, ulong generation = Generation, byte[]? key = null) =>
            Packet.TryVerify(envelope ?? Envelope, context ?? Context, generation, key ?? PublicKey, out _);
    }

    [Theory]
    [InlineData(0, 2, 3, false)] [InlineData(8, 2, 2, true)]
    [InlineData(16, 3, 5, false)] [InlineData(24, 3, 3, true)]
    [InlineData(31, 5, 5, false)] [InlineData(32, 3, 5, true)]
    [InlineData(62, 9, 9, false)] [InlineData(63, 3, 5, false)]
    [InlineData(128, 10, 10, true)] [InlineData(1024, 3, 3, false)]
    public void AuthenticatedVerificationAndRecoveryPreserveAllBytes(int length, int threshold, int count, bool sparse)
    {
        var f = new Fixture(length, threshold, count, sparse);
        Assert.True(Packet.TryVerify(f.Envelope, f.Context, Fixture.Generation, f.PublicKey, out var commitments));
        Assert.True(commitments.SequenceEqual(f.Commitments));
        Assert.True(f.Envelope.AsSpan().Overlaps(commitments)); // A slice, not a copy.
        Assert.True(Packet.VerifyShare(f.Shares.AsSpan(0, f.ShareLength), f.Envelope, f.Context, Fixture.Generation, f.PublicKey));
        byte[] output = Enumerable.Repeat((byte)0xA5, length + 7).ToArray();
        Assert.True(Packet.TryReconstruct(f.Shares, f.Envelope, f.Context, Fixture.Generation, f.PublicKey, output, out int written));
        Assert.Equal(length, written); Assert.True(output.AsSpan(0, length).SequenceEqual(f.Secret));
        Assert.True(output.AsSpan(length).IndexOfAnyExcept((byte)0xA5) < 0);
        Assert.Equal(length, Packet.Reconstruct(f.Shares, f.Envelope, f.Context, Fixture.Generation, f.PublicKey, output));
    }

    [Theory]
    [InlineData(0, 0UL)] [InlineData(1, 1UL)] [InlineData(255, ulong.MaxValue)] [InlineData(4096, 42UL)]
    public void ArbitraryContextAndGenerationHaveUnambiguousDeterministicEncoding(int contextLength, ulong generation)
    {
        var f = new Fixture(0, 2, 2);
        byte[] context = Enumerable.Range(0, contextLength).Select(i => (byte)(i & 255)).ToArray();
        byte[] output = Enumerable.Repeat((byte)0xA5, Packet.GetPacketLength(f.Commitments.Length, context.Length) + 7).ToArray();
        int length = Packet.Create(f.Commitments, context, generation, f.Seed, output);
        Assert.True(output.AsSpan(0, 32).SequenceEqual("Mrbr.Cryptography.Vss.AuthPacket"u8));
        Assert.Equal(1, output[32]); Assert.Equal(1, output[33]); Assert.Equal(0, output[34]); Assert.Equal(0, output[35]);
        Assert.Equal((uint)contextLength, BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(36)));
        Assert.Equal((uint)f.Commitments.Length, BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(40)));
        Assert.Equal(generation, BinaryPrimitives.ReadUInt64LittleEndian(output.AsSpan(44)));
        Assert.True(output.AsSpan(52, contextLength).SequenceEqual(context));
        Assert.True(output.AsSpan(52 + contextLength, f.Commitments.Length).SequenceEqual(f.Commitments));
        Assert.True(output.AsSpan(length).IndexOfAnyExcept((byte)0xA5) < 0);
        Assert.True(Packet.TryVerify(output.AsSpan(0, length), context, generation, f.PublicKey, out _));
        Assert.False(Packet.TryVerify(output, context, generation, f.PublicKey, out _));
        byte[] second = new byte[length];
        Packet.Create(f.Commitments, context, generation, f.Seed, second);
        Assert.True(output.AsSpan(0, length).SequenceEqual(second));
        Assert.Equal(Ed25519Reference.Sign(f.Seed, second[..^64]), second[^64..]);
    }

    [Fact]
    public void EverySingleByteMutationAndEveryTruncationAreRejectedWithoutPublishingOutput()
    {
        var f = new Fixture(8, 3, 3);
        byte[] mutated = f.Envelope.ToArray(), output = Enumerable.Repeat((byte)0xA5, 15).ToArray();
        for (int i = 0; i < mutated.Length; i++)
        {
            mutated[i] ^= 1;
            Assert.False(Packet.TryVerify(mutated, f.Context, Fixture.Generation, f.PublicKey, out var inner));
            Assert.True(inner.IsEmpty);
            Assert.False(Packet.TryReconstruct(f.Shares, mutated, f.Context, Fixture.Generation, f.PublicKey, output, out int written));
            Assert.Equal(0, written); Assert.True(output.AsSpan().IndexOfAnyExcept((byte)0xA5) < 0);
            mutated[i] ^= 1;
        }
        for (int length = 0; length < f.Envelope.Length; length++)
            Assert.False(Packet.TryVerify(f.Envelope.AsSpan(0, length), f.Context, Fixture.Generation, f.PublicKey, out _));
    }

    [Fact]
    public void WrongSignerContextAndGenerationAreRejected()
    {
        var f = new Fixture();
        byte[] seed = new byte[32], otherKey = new byte[32]; Packet.DerivePublicKey(seed, otherKey);
        Assert.False(f.Verify(key: otherKey));
        Assert.False(f.Verify(context: []));
        byte[] wrongContext = f.Context.ToArray(); wrongContext[0] ^= 1;
        Assert.False(f.Verify(context: wrongContext));
        Assert.False(f.Verify(generation: Fixture.Generation - 1));
        Assert.False(f.Verify(generation: Fixture.Generation + 1));
        Assert.True(f.Verify()); // Freshness belongs to the caller's expected generation.
        byte[] old = new byte[f.Envelope.Length];
        Packet.Create(f.Commitments, f.Context, Fixture.Generation - 1, f.Seed, old);
        Assert.False(f.Verify(envelope: old));
        Assert.True(f.Verify(envelope: old, generation: Fixture.Generation - 1));
    }

    [Fact]
    public void ReplacingBothSharesAndCommitmentsStillRequiresTheTrustedSigningSeed()
    {
        var trusted = new Fixture();
        var replacement = new Fixture();
        byte[] attackerSeed = new byte[32];
        Packet.Create(replacement.Commitments, trusted.Context, Fixture.Generation, attackerSeed, replacement.Envelope);
        Assert.True(PedersenVss.VerifyShare(replacement.Shares.AsSpan(0, replacement.ShareLength), replacement.Commitments));
        Assert.False(Packet.VerifyShare(replacement.Shares.AsSpan(0, replacement.ShareLength), replacement.Envelope,
            trusted.Context, Fixture.Generation, trusted.PublicKey));
        Assert.False(Packet.VerifyShare(replacement.Shares.AsSpan(0, replacement.ShareLength), trusted.Envelope,
            trusted.Context, Fixture.Generation, trusted.PublicKey));
    }

    [Fact]
    public void SignedMalformedVssValuesStillFailVssValidation()
    {
        var f = new Fixture();
        f.Commitments.AsSpan(44, 32).Fill(255); // Framed but invalid Ristretto encoding, signed by a valid dealer.
        Packet.Create(f.Commitments, f.Context, Fixture.Generation, f.Seed, f.Envelope);
        Assert.True(f.Verify()); // Signature validation does not replace group validation.
        Assert.False(Packet.VerifyShare(f.Shares.AsSpan(0, f.ShareLength), f.Envelope, f.Context, Fixture.Generation, f.PublicKey));
        AssertUnchangedOnFailure(f, f.Shares);
    }

    [Fact]
    public void CorruptAndDuplicateSharesAndInvalidExtrasAreRejected()
    {
        var f = new Fixture();
        for (int share = 0; share < 5; share++)
        {
            byte[] changed = f.Shares.ToArray(); changed[share * f.ShareLength + 33] ^= 1;
            AssertUnchangedOnFailure(f, changed);
        }
        byte[] duplicate = f.Shares.ToArray(); duplicate.AsSpan(0, f.ShareLength).CopyTo(duplicate.AsSpan(f.ShareLength));
        AssertUnchangedOnFailure(f, duplicate);
        AssertUnchangedOnFailure(f, f.Shares[..(2 * f.ShareLength)]);
        AssertUnchangedOnFailure(f, f.Shares[..^1]);
        Assert.Throws<CryptographicException>(() => Packet.Reconstruct(duplicate, f.Envelope, f.Context, Fixture.Generation, f.PublicKey, new byte[32]));
    }

    private static void AssertUnchangedOnFailure(Fixture f, byte[] shares)
    {
        byte[] output = Enumerable.Repeat((byte)0xA5, f.Secret.Length + 7).ToArray();
        Assert.False(Packet.TryReconstruct(shares, f.Envelope, f.Context, Fixture.Generation, f.PublicKey, output, out int written));
        Assert.Equal(0, written); Assert.True(output.AsSpan().IndexOfAnyExcept((byte)0xA5) < 0);
    }

    [Fact]
    public void UntrustedLengthFieldsCannotOverflowOrEscapeThePacket()
    {
        var f = new Fixture(0, 2, 2);
        foreach (int field in new[] { 36, 40 })
            foreach (uint length in new uint[] { 0, 1, 43, 44, int.MaxValue, (uint)int.MaxValue + 1, uint.MaxValue })
            {
                if (length == BinaryPrimitives.ReadUInt32LittleEndian(f.Envelope.AsSpan(field))) continue;
                byte[] changed = f.Envelope.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(field), length);
                Assert.False(f.Verify(envelope: changed));
            }
        var random = new Random(938246);
        for (int i = 0; i < 1000; i++)
        {
            byte[] packet = new byte[random.Next(0, 2048)]; random.NextBytes(packet);
            Assert.False(f.Verify(envelope: packet));
        }
    }

    [Fact]
    public void MatchingExpectedMetadataDoesNotAuthorizeAnUnsignedModification()
    {
        var f = new Fixture();
        byte[] changed = f.Envelope.ToArray(), changedContext = f.Context.ToArray();
        changed[52] ^= 1; changedContext[0] ^= 1;
        Assert.False(f.Verify(envelope: changed, context: changedContext));
        changed = f.Envelope.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(changed.AsSpan(44), Fixture.Generation + 1);
        Assert.False(f.Verify(envelope: changed, generation: Fixture.Generation + 1));
        changed = f.Envelope.ToArray();
        changed[52 + f.Context.Length + 12] ^= 1; // Inner share-set identifier remains structurally valid.
        Assert.False(f.Verify(envelope: changed));
    }
}
