using System.Security.Cryptography;
using Mrbr.Cryptography.Vss.Internal;

namespace Mrbr.Cryptography.Vss.Tests;

public sealed class PedersenVssTests
{
    [Theory]
    [InlineData(0, 2, 3)]
    [InlineData(1, 2, 3)]
    [InlineData(8, 3, 5)]
    [InlineData(16, 3, 5)]
    [InlineData(24, 3, 5)]
    [InlineData(30, 3, 5)]
    [InlineData(31, 3, 5)]
    [InlineData(32, 3, 5)]
    [InlineData(33, 3, 5)]
    [InlineData(61, 3, 5)]
    [InlineData(62, 3, 5)]
    [InlineData(63, 3, 5)]
    [InlineData(93, 5, 7)]
    [InlineData(32, 9, 9)]
    [InlineData(1024, 3, 5)]
    [InlineData(4097, 2, 3)]
    public void AnyBytesRoundTripFromDenseReorderedSparseAndAllShares(int length, int threshold, int count)
    {
        byte[] secret = RandomNumberGenerator.GetBytes(length);
        if (length > 0) secret[^1] = 0; // Length preservation includes trailing zeros.
        var (shares, commitments) = Create(secret, threshold, count);
        Assert.True(PedersenVss.TryReadParameters(commitments, out int originalLength, out int originalThreshold, out int originalCount));
        Assert.Equal(length, originalLength); Assert.Equal(threshold, originalThreshold); Assert.Equal(count, originalCount);
        int shareLength = PedersenVss.GetShareLength(length);
        for (int i = 0; i < count; i++) Assert.True(PedersenVss.VerifyShare(shares.AsSpan(i * shareLength, shareLength), commitments));
        byte[] output = Enumerable.Repeat((byte)0xA5, length + 3).ToArray();
        foreach (byte[] selected in new[] { shares[..(threshold * shareLength)], Select(shares, shareLength, threshold, count), shares })
        {
            Assert.True(PedersenVss.TryReconstruct(selected, commitments, output, out int written));
            Assert.Equal(length, written);
            Assert.Equal(secret, output[..length]);
            Assert.All(output[length..], value => Assert.Equal(0xA5, value));
            Assert.Equal(length, PedersenVss.Reconstruct(selected, commitments, output));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(9)]
    public void SparseCoordinatesInclude255WithSingleDigitQuorums(int threshold)
    {
        byte[] secret = [0, 1, 2, 3, 0, 255, 128, 0];
        var (shares, commitments) = Create(secret, threshold, 255);
        int length = PedersenVss.GetShareLength(secret.Length);
        byte[] selected = new byte[threshold * length];
        for (int i = 0; i < threshold; i++) shares.AsSpan(((254 + 73 * i) % 255) * length, length).CopyTo(selected.AsSpan(i * length));
        byte[] output = new byte[secret.Length];
        Assert.Equal(secret.Length, PedersenVss.Reconstruct(selected, commitments, output));
        Assert.Equal(secret, output);
    }

    [Fact]
    public void MaximumThresholdAndIdentityCommitmentsAreSupported()
    {
        var (emptyShares, emptyCommitments) = Create([], 255, 255);
        Assert.Equal(0, PedersenVss.Reconstruct(emptyShares, emptyCommitments, []));
        byte[] shares = new byte[PedersenVss.GetSharesLength(1, 255)];
        byte[] commitments = new byte[PedersenVss.GetCommitmentsLength(1, 255)];
        PedersenVss.CreateShares(new byte[1], 255, 255, shares, commitments, new ZeroRandom());
        Assert.All(commitments[44..], value => Assert.Equal(0, value));
        Assert.True(PedersenVss.VerifyShare(shares.AsSpan(254 * 97, 97), commitments));
    }

    [Fact]
    public void DeterministicPolynomialsMatchIndependentPrimeFieldEvaluation()
    {
        byte[] secret = Enumerable.Repeat((byte)255, 31).ToArray();
        byte[] shares = new byte[97 * 5], commitments = new byte[44 + 3 * 32];
        var random = new CountingRandom();
        PedersenVss.CreateShares(secret, 3, 5, shares, commitments, random);
        // RNG call 1 is the set ID; a1=2,a2=3,b0=4,b1=5,b2=6.
        for (int i = 0; i < 5; i++)
        {
            int x = i + 1;
            Assert.Equal((PrimitiveTests.Number(secret) + 2 * x + 3 * x * x) % PrimitiveTests.Order, PrimitiveTests.Number(shares.AsSpan(i * 97 + 33, 32)));
            Assert.Equal(4 + 5 * x + 6 * x * x, PrimitiveTests.Number(shares.AsSpan(i * 97 + 65, 32)));
            Assert.True(PedersenVss.VerifyShare(shares.AsSpan(i * 97, 97), commitments));
        }
    }

    [Fact]
    public void EveryThreeOfFiveSubsetAndPermutationReconstructs()
    {
        byte[] secret = [42, 0, 255];
        var (shares, commitments) = Create(secret, 3, 5);
        const int length = 97;
        byte[] selected = new byte[length * 3], output = new byte[3];
        for (int a = 0; a < 5; a++) for (int b = 0; b < 5; b++) for (int c = 0; c < 5; c++)
        {
            if (a == b || a == c || b == c) continue;
            shares.AsSpan(a * length, length).CopyTo(selected);
            shares.AsSpan(b * length, length).CopyTo(selected.AsSpan(length));
            shares.AsSpan(c * length, length).CopyTo(selected.AsSpan(length * 2));
            Assert.Equal(3, PedersenVss.Reconstruct(selected, commitments, output));
            Assert.Equal(secret, output);
        }
    }

    [Theory]
    [InlineData("y")]
    [InlineData("z")]
    [InlineData("coordinate")]
    [InlineData("digest")]
    [InlineData("noncanonical-y")]
    [InlineData("noncanonical-z")]
    [InlineData("valid-point")]
    [InlineData("invalid-point")]
    [InlineData("set-id")]
    public void CorruptionIsRejectedWithoutChangingDestination(string part)
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        var (shares, commitments) = Create(secret, 3, 5);
        int shareLength = PedersenVss.GetShareLength(secret.Length);
        switch (part)
        {
            case "y": shares[33] ^= 1; break;
            case "z": shares[65] ^= 1; break;
            case "coordinate": shares[32] = 2; break;
            case "digest": shares[0] ^= 1; break;
            case "noncanonical-y": Ristretto.Order.CopyTo(shares.AsSpan(33, 32)); break;
            case "noncanonical-z": Ristretto.Order.CopyTo(shares.AsSpan(65, 32)); break;
            case "invalid-point": commitments.AsSpan(44, 32).Fill(255); break;
            case "set-id": commitments[12] ^= 1; break;
            case "valid-point":
                byte[] delta = new byte[32], changed = new byte[32];
                Ristretto.MultiplyBase(PrimitiveTests.Encode(1), delta);
                Sodium.PointAdd(changed, commitments.AsSpan(44, 32), delta);
                changed.CopyTo(commitments, 44);
                RebindDigests(shares, commitments, shareLength); // Force the curve equation to detect this.
                break;
        }
        Assert.False(PedersenVss.VerifyShare(shares.AsSpan(0, shareLength), commitments));
        byte[] output = Enumerable.Repeat((byte)0xA5, 35).ToArray();
        Assert.False(PedersenVss.TryReconstruct(shares.AsSpan(0, shareLength * 3), commitments, output, out int written));
        Assert.Equal(0, written);
        Assert.All(output, value => Assert.Equal(0xA5, value));
        Assert.Throws<CryptographicException>(() => PedersenVss.Reconstruct(shares.AsSpan(0, shareLength * 3), commitments, output));
    }

    [Fact]
    public void IndividuallyVerifiableButNonByteEncodableSecretIsRejectedBeforeAnyWrites()
    {
        var (shares, commitments) = Create(new byte[32], 2, 3);
        int length = PedersenVss.GetShareLength(32);
        byte[] a = new byte[64], b = new byte[64], h = new byte[32];
        Ristretto.DeriveH(h);
        a[0] = 42; a[32] = 2; b[0] = 3; b[32] = 4;
        for (int block = 0; block < 2; block++)
        {
            if (block == 1) { a[0] = 0; a[1] = 1; } // Second block encodes one byte, but constant is 256.
            for (int j = 0; j < 2; j++) Ristretto.Commit(a.AsSpan(j * 32, 32), b.AsSpan(j * 32, 32), h, commitments.AsSpan(44 + (block * 2 + j) * 32, 32));
            for (int i = 0; i < 3; i++)
            {
                Ristretto.Evaluate(a, (byte)(i + 1), shares.AsSpan(i * length + 33 + block * 64, 32));
                Ristretto.Evaluate(b, (byte)(i + 1), shares.AsSpan(i * length + 65 + block * 64, 32));
            }
        }
        RebindDigests(shares, commitments, length);
        Assert.True(PedersenVss.VerifyShare(shares.AsSpan(0, length), commitments));
        byte[] output = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        Assert.False(PedersenVss.TryReconstruct(shares.AsSpan(0, length * 2), commitments, output, out int written));
        Assert.Equal(0, written);
        Assert.All(output, value => Assert.Equal(0xA5, value));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 2)]
    [InlineData(5, 0)]
    [InlineData(6, 1)]
    [InlineData(7, 1)]
    [InlineData(11, 255)]
    public void MalformedHeadersAreRejected(int offset, byte value)
    {
        var (shares, commitments) = Create(new byte[8], 3, 5);
        commitments[offset] = value;
        Assert.False(PedersenVss.VerifyShare(shares.AsSpan(0, 97), commitments));
        Assert.False(PedersenVss.TryReconstruct(shares, commitments, new byte[8], out _));
    }

    [Fact]
    public void MixedSetsDuplicatesInsufficientSharesAndBadFramingAreRejected()
    {
        var (shares, commitments) = Create(new byte[8], 3, 5);
        var (other, otherCommitments) = Create(new byte[8], 3, 5);
        Assert.NotEqual(commitments, otherCommitments);
        Assert.False(PedersenVss.VerifyShare(shares.AsSpan(0, 97), otherCommitments));
        byte[] output = new byte[8];
        Assert.False(PedersenVss.TryReconstruct(shares.AsSpan(0, 194), commitments, output, out _));
        Assert.False(PedersenVss.TryReconstruct(shares.AsSpan(0, 290), commitments, output, out _));
        Assert.False(PedersenVss.VerifyShare(shares.AsSpan(0, 96), commitments));
        Assert.False(PedersenVss.VerifyShare(shares.AsSpan(0, 98), commitments));
        other.AsSpan(0, 97).CopyTo(shares.AsSpan(194, 97));
        Assert.False(PedersenVss.TryReconstruct(shares, commitments, output, out _));
        shares.AsSpan(0, 97).CopyTo(shares.AsSpan(194, 97));
        Assert.False(PedersenVss.TryReconstruct(shares, commitments, output, out _));
        shares[32] = 0;
        Assert.False(PedersenVss.VerifyShare(shares.AsSpan(0, 97), commitments));
        shares[32] = 6;
        Assert.False(PedersenVss.VerifyShare(shares.AsSpan(0, 97), commitments));
    }

    [Fact]
    public void AllSuppliedSharesMustVerifyIncludingExtrasBeyondThreshold()
    {
        var (shares, commitments) = Create(new byte[8], 3, 5);
        shares[4 * 97 + 65] ^= 1;
        byte[] output = Enumerable.Repeat((byte)0xA5, 8).ToArray();
        Assert.True(PedersenVss.TryReconstruct(shares.AsSpan(0, 3 * 97), commitments, output, out _));
        Array.Fill(output, (byte)0xA5);
        Assert.False(PedersenVss.TryReconstruct(shares, commitments, output, out _));
        Assert.All(output, value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void CallerErrorsPrecedeWritesAndRngAndSuffixesArePreserved()
    {
        byte[] secret = new byte[8];
        byte[] shares = Enumerable.Repeat((byte)0xA5, 97 * 5 + 3).ToArray();
        byte[] commitments = Enumerable.Repeat((byte)0xA5, 44 + 3 * 32 + 3).ToArray();
        var random = new CountingRandom();
        Assert.Throws<ArgumentOutOfRangeException>(() => PedersenVss.CreateShares(secret, 6, 5, shares, commitments, random));
        Assert.Throws<ArgumentException>(() => PedersenVss.CreateShares(secret, 3, 5, shares.AsSpan(0, 97), commitments, random));
        Assert.Throws<ArgumentException>(() => PedersenVss.CreateShares(secret, 3, 5, shares, commitments.AsSpan(0, 44), random));
        Assert.Throws<ArgumentException>(() => PedersenVss.CreateShares(secret, 3, 5, shares, shares.AsSpan(0, 140), random));
        Assert.Throws<ArgumentException>(() => PedersenVss.CreateShares(shares.AsSpan(0, 8), 3, 5, shares, commitments, random));
        Assert.Equal(0, random.Calls);
        Assert.All(shares, value => Assert.Equal(0xA5, value));
        Assert.All(commitments, value => Assert.Equal(0xA5, value));
        PedersenVss.CreateShares(secret, 3, 5, shares, commitments);
        Assert.All(shares[(97 * 5)..], value => Assert.Equal(0xA5, value));
        Assert.All(commitments[140..], value => Assert.Equal(0xA5, value));
        Assert.Throws<ArgumentException>(() => PedersenVss.TryReconstruct(shares.AsSpan(0, 3 * 97), commitments.AsSpan(0, 140), new byte[7], out _));
        Assert.Throws<ArgumentException>(() => PedersenVss.TryReconstruct(shares.AsSpan(0, 3 * 97), commitments.AsSpan(0, 140), shares.AsSpan(0, 8), out _));
        Assert.Throws<ArgumentException>(() => PedersenVss.TryReconstruct(shares.AsSpan(0, 3 * 97), commitments.AsSpan(0, 140), commitments.AsSpan(0, 8), out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void RngFailureWipesBothUsedPrefixesAndPreservesSuffixes(int failAt)
    {
        byte[] shares = Enumerable.Repeat((byte)0xA5, 161 * 3 + 3).ToArray();
        byte[] commitments = Enumerable.Repeat((byte)0xA5, 44 + 2 * 2 * 32 + 3).ToArray();
        Assert.Throws<CryptographicException>(() => PedersenVss.CreateShares(new byte[32], 2, 3, shares, commitments, new CountingRandom(failAt)));
        Assert.All(shares[..(161 * 3)], value => Assert.Equal(0, value));
        Assert.All(commitments[..172], value => Assert.Equal(0, value));
        Assert.All(shares[(161 * 3)..], value => Assert.Equal(0xA5, value));
        Assert.All(commitments[172..], value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void SizingRejectsInvalidCountsLengthsAndOverflow()
    {
        Assert.Equal(33, PedersenVss.GetShareLength(0));
        Assert.Equal(97, PedersenVss.GetShareLength(31));
        Assert.Equal(161, PedersenVss.GetShareLength(32));
        Assert.Equal(236, PedersenVss.GetCommitmentsLength(32, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PedersenVss.GetShareLength(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PedersenVss.GetSharesLength(8, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PedersenVss.GetSharesLength(8, 256));
        Assert.Throws<ArgumentOutOfRangeException>(() => PedersenVss.GetCommitmentsLength(8, 1));
        Assert.Throws<OverflowException>(() => PedersenVss.GetShareLength(int.MaxValue));
        Assert.Throws<OverflowException>(() => PedersenVss.GetSharesLength(100_000_000, 255));
        Assert.Throws<OverflowException>(() => PedersenVss.GetCommitmentsLength(int.MaxValue, 255));
    }

    internal static (byte[] Shares, byte[] Commitments) Create(byte[] secret, int threshold, int count)
    {
        byte[] shares = new byte[PedersenVss.GetSharesLength(secret.Length, count)];
        byte[] commitments = new byte[PedersenVss.GetCommitmentsLength(secret.Length, threshold)];
        Assert.Equal(shares.Length, PedersenVss.CreateShares(secret, threshold, count, shares, commitments));
        return (shares, commitments);
    }

    internal static byte[] Select(byte[] shares, int length, int threshold, int count)
    {
        byte[] selected = new byte[length * threshold];
        for (int i = 0; i < threshold; i++)
        {
            int index = (i & 1) == 0 ? count - 1 - i / 2 : i / 2;
            shares.AsSpan(index * length, length).CopyTo(selected.AsSpan(i * length));
        }
        return selected;
    }

    private static void RebindDigests(byte[] shares, byte[] commitments, int shareLength)
    {
        byte[] hash = SHA256.HashData(commitments);
        for (int i = 0; i < shares.Length / shareLength; i++) hash.CopyTo(shares, i * shareLength);
    }

    private sealed class ZeroRandom : IRandomSource { public void Fill(Span<byte> destination) => destination.Clear(); }
    private sealed class CountingRandom(int failAt = int.MaxValue) : IRandomSource
    {
        internal int Calls;
        public void Fill(Span<byte> destination)
        {
            if (++Calls == failAt) throw new CryptographicException("Test RNG failure.");
            destination.Clear(); destination[0] = (byte)(Calls & 255);
        }
    }
}
