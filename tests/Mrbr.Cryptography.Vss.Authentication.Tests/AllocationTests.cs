using Packet = Mrbr.Cryptography.Vss.Authentication.AuthenticatedCommitmentPacket;

namespace Mrbr.Cryptography.Vss.Authentication.Tests;

public sealed class AllocationTests
{
    [Theory]
    [InlineData(0, 2, false)] [InlineData(8, 2, true)] [InlineData(32, 3, false)]
    [InlineData(32, 5, true)] [InlineData(32, 9, false)] [InlineData(1024, 3, false)]
    public void WarmPublicOperationsAndUntrustedFailuresAllocateZeroManagedBytes(int length, int count, bool sparse)
    {
        var f = new PacketTests.Fixture(length, count, count, sparse);
        byte[] output = new byte[length], publicKey = new byte[32], seed = new byte[32], badPacket = f.Envelope.ToArray();
        badPacket[^1] ^= 1;
        for (int warm = 0; warm < 5; warm++)
        {
            Packet.GenerateSigningSeed(seed); Packet.DerivePublicKey(f.Seed, publicKey);
            Packet.Create(f.Commitments, f.Context, PacketTests.Fixture.Generation, f.Seed, f.Envelope);
            Packet.TryVerify(f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, out _);
            Packet.VerifyShare(f.Shares.AsSpan(0, f.ShareLength), f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey);
            Packet.TryReconstruct(f.Shares, f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output, out _);
            Packet.Reconstruct(f.Shares, f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output);
            Packet.TryReconstruct(f.Shares, badPacket, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output, out _);
        }
        _ = GC.GetAllocatedBytesForCurrentThread();
        for (int pass = 0; pass < 3; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Packet.GenerateSigningSeed(seed); Packet.DerivePublicKey(f.Seed, publicKey);
            int created = Packet.Create(f.Commitments, f.Context, PacketTests.Fixture.Generation, f.Seed, f.Envelope);
            bool verified = Packet.TryVerify(f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, out _);
            bool share = Packet.VerifyShare(f.Shares.AsSpan(0, f.ShareLength), f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey);
            bool reconstructed = Packet.TryReconstruct(f.Shares, f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output, out int written);
            int recovered = Packet.Reconstruct(f.Shares, f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, allocated); Assert.Equal(f.Envelope.Length, created);
            Assert.True(verified); Assert.True(share); Assert.True(reconstructed); Assert.Equal(length, written); Assert.Equal(length, recovered);
            Assert.Equal(f.Secret, output);

            before = GC.GetAllocatedBytesForCurrentThread();
            bool invalidSignature = Packet.TryVerify(badPacket, f.Context, PacketTests.Fixture.Generation, f.PublicKey, out _);
            bool invalidRecovery = Packet.TryReconstruct(f.Shares, badPacket, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output, out int badWritten);
            bool invalidContext = Packet.TryVerify(f.Envelope, [], PacketTests.Fixture.Generation, f.PublicKey, out _);
            long failureAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, failureAllocated); Assert.False(invalidSignature); Assert.False(invalidRecovery); Assert.False(invalidContext); Assert.Equal(0, badWritten);

            f.Shares[0] ^= 1;
            before = GC.GetAllocatedBytesForCurrentThread();
            bool invalidShare = Packet.VerifyShare(f.Shares.AsSpan(0, f.ShareLength), f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey);
            bool invalidQuorum = Packet.TryReconstruct(f.Shares, f.Envelope, f.Context, PacketTests.Fixture.Generation, f.PublicKey, output, out _);
            long vssFailureAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            f.Shares[0] ^= 1;
            Assert.Equal(0L, vssFailureAllocated); Assert.False(invalidShare); Assert.False(invalidQuorum);
        }
    }
}
