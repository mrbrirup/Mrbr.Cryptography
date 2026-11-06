using Mrbr.Cryptography.Internal;

namespace Mrbr.Cryptography.Tests;

public sealed class ShamirSharedSecretSimdTests
{
    [Theory]
    [InlineData(0, 3, 5)]
    [InlineData(1, 3, 5)]
    [InlineData(15, 3, 5)]
    [InlineData(16, 3, 5)]
    [InlineData(17, 3, 5)]
    [InlineData(31, 3, 5)]
    [InlineData(32, 3, 5)]
    [InlineData(33, 3, 5)]
    [InlineData(47, 3, 5)]
    [InlineData(48, 3, 5)]
    [InlineData(49, 3, 5)]
    [InlineData(63, 3, 5)]
    [InlineData(64, 3, 5)]
    [InlineData(65, 3, 5)]
    [InlineData(2047, 3, 5)]
    [InlineData(2048, 3, 5)]
    [InlineData(2049, 3, 5)]
    [InlineData(4095, 2, 3)]
    [InlineData(4096, 2, 3)]
    [InlineData(4097, 2, 3)]
    [InlineData(585, 8, 11)]
    [InlineData(586, 8, 11)]
    [InlineData(15, 255, 255)]
    [InlineData(16, 255, 255)]
    [InlineData(17, 255, 255)]
    [InlineData(31, 255, 255)]
    [InlineData(32, 255, 255)]
    [InlineData(33, 255, 255)]
    public void AllImplementations_MatchScalarSharesAndReconstruction_AtBoundaries(int length, int threshold, int count)
    {
        byte[] input = new byte[length + 7];
        input.AsSpan().Fill(0xCC);
        for (int position = 0; position < length; position++)
        {
            input[position + 1] = (byte)(((position * 173) + 0x81) & 0xFF);
        }

        byte[] originalInput = (byte[])input.Clone();
        int shareLength = ShamirSharedSecret.GetShareLength(length);
        int packedLength = ShamirSharedSecret.GetSharesLength(length, count);
        byte[] expectedShares = new byte[packedLength];
        var scalarSource = new DeterministicRandomSource();
        ShamirSharedSecret.CreateShares(input.AsSpan(1, length), threshold, count,
            expectedShares, scalarSource, Gf256Implementation.Scalar);

        byte[] selected = new byte[threshold * shareLength];
        for (int index = 0; index < threshold; index++)
        {
            int share = (index & 1) == 0 ? count - 1 - (index / 2) : index / 2;
            expectedShares.AsSpan(share * shareLength, shareLength).CopyTo(selected.AsSpan(index * shareLength));
        }

        byte[] originalSelected = (byte[])selected.Clone();
        foreach (Gf256Implementation implementation in Gf256SimdTests.SupportedImplementations())
        {
            byte[] actualShares = new byte[packedLength + 9];
            actualShares.AsSpan().Fill(0xCC);
            var source = new DeterministicRandomSource();

            Assert.Equal(packedLength, ShamirSharedSecret.CreateShares(input.AsSpan(1, length), threshold, count,
                actualShares.AsSpan(3, packedLength + 2), source, implementation));

            Assert.True(expectedShares.AsSpan().SequenceEqual(actualShares.AsSpan(3, packedLength)));
            Assert.All(actualShares[..3], value => Assert.Equal((byte)0xCC, value));
            Assert.All(actualShares[(packedLength + 3)..], value => Assert.Equal((byte)0xCC, value));
            Assert.Equal(length * (threshold - 1), source.BytesConsumed);
            Assert.Equal(scalarSource.BytesConsumed, source.BytesConsumed);

            byte[] recovered = new byte[length + 9];
            recovered.AsSpan().Fill(0xCC);
            Assert.Equal(length, ShamirSharedSecret.Reconstruct(selected, shareLength, threshold,
                recovered.AsSpan(2, length + 2), implementation));

            Assert.True(input.AsSpan(1, length).SequenceEqual(recovered.AsSpan(2, length)));
            Assert.All(recovered[..2], value => Assert.Equal((byte)0xCC, value));
            Assert.All(recovered[(length + 2)..], value => Assert.Equal((byte)0xCC, value));
        }

        Assert.Equal(originalInput, input);
        Assert.Equal(originalSelected, selected);
    }

    [Fact]
    public void VectorCreation_AcceptsAllZeroCoefficients_WithoutResampling()
    {
        byte[] secret = new byte[65];
        for (int position = 0; position < secret.Length; position++)
        {
            secret[position] = (byte)((position * 47) & 0xFF);
        }

        int shareLength = ShamirSharedSecret.GetShareLength(secret.Length);
        foreach (Gf256Implementation implementation in Gf256SimdTests.SupportedImplementations())
        {
            byte[] shares = new byte[ShamirSharedSecret.GetSharesLength(secret.Length, 7)];
            var source = new ZeroRandomSource();
            ShamirSharedSecret.CreateShares(secret, 5, 7, shares, source, implementation);

            for (int share = 0; share < 7; share++)
            {
                Assert.True(secret.AsSpan().SequenceEqual(shares.AsSpan((share * shareLength) + 1, secret.Length)));
            }

            Assert.Equal(secret.Length * 4, source.BytesConsumed);
        }
    }

    [Fact]
    public void ForcedImplementations_AllocateNoManagedMemory_AfterWarmup()
    {
        byte[] secret = new byte[4097];
        byte[] shares = new byte[ShamirSharedSecret.GetSharesLength(secret.Length, 5)];
        byte[] recovered = new byte[secret.Length];
        int shareLength = ShamirSharedSecret.GetShareLength(secret.Length);
        foreach (Gf256Implementation implementation in Gf256SimdTests.SupportedImplementations())
        {
            var source = new DeterministicRandomSource();
            ShamirSharedSecret.CreateShares(secret, 3, 5, shares, source, implementation);
            ShamirSharedSecret.Reconstruct(shares, shareLength, 3, recovered, implementation);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long beforeCreation = GC.GetAllocatedBytesForCurrentThread();
            int created = ShamirSharedSecret.CreateShares(secret, 3, 5, shares, source, implementation);
            long creationAllocations = GC.GetAllocatedBytesForCurrentThread() - beforeCreation;
            long beforeReconstruction = GC.GetAllocatedBytesForCurrentThread();
            int reconstructed = ShamirSharedSecret.Reconstruct(shares, shareLength, 3, recovered, implementation);
            long reconstructionAllocations = GC.GetAllocatedBytesForCurrentThread() - beforeReconstruction;

            Assert.Equal(shares.Length, created);
            Assert.Equal(secret.Length, reconstructed);
            Assert.Equal(secret, recovered);
            Assert.Equal(0L, creationAllocations);
            Assert.Equal(0L, reconstructionAllocations);
        }
    }

    private sealed class DeterministicRandomSource : IRandomSource
    {
        private uint _state = 0xA57C9035;
        public int BytesConsumed { get; private set; }

        public void Fill(Span<byte> destination)
        {
            // Test-only xorshift stream avoids a short repeating byte pattern.
            for (int index = 0; index < destination.Length; index++)
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                destination[index] = unchecked((byte)_state);
            }

            BytesConsumed += destination.Length;
        }
    }

    private sealed class ZeroRandomSource : IRandomSource
    {
        public int BytesConsumed { get; private set; }

        public void Fill(Span<byte> destination)
        {
            destination.Clear();
            BytesConsumed += destination.Length;
        }
    }
}
