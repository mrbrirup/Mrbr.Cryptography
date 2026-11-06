using System.Numerics;
using System.Security.Cryptography;
using Mrbr.Cryptography.Vss.Internal;
using Mrbr.Cryptography.Vss.OptimizationBenchmarks.Baselines;

namespace Mrbr.Cryptography.Vss.Tests;

public sealed class OptimizationTests
{
    public static IEnumerable<object[]> CoordinateCases()
    {
        for (int count = 2; count <= 9; count++)
            foreach (bool sparse in new[] { false, true }) yield return [count, sparse];
    }

    [Fact]
    public void CoordinateMultiplicationMatchesFullNativeOperationForEveryByte()
    {
        byte[] h = new byte[32], generator = new byte[32], other = new byte[32];
        Ristretto.DeriveH(h);
        Ristretto.MultiplyBase(PrimitiveTests.Encode(1), generator);
        Ristretto.MultiplyBase(PrimitiveTests.Encode(7231), other);
        byte[] result = new byte[32], expected = new byte[32], scalar = new byte[32];
        foreach (byte[] point in new[] { h, generator, other, new byte[32] })
            for (int coordinate = 0; coordinate <= 255; coordinate++)
            {
                scalar[0] = (byte)coordinate;
                Ristretto.MultiplyPublicPoint(scalar, point, expected);
                Ristretto.MultiplyByCoordinate((byte)coordinate, point, result);
                Assert.Equal(expected, result);
            }
    }

    [Theory, MemberData(nameof(CoordinateCases))]
    public void FullLagrangeBasisMatchesIndependentBigIntegerCoefficients(int count, bool sparse)
    {
        byte[] coordinates = Coordinates(count, sparse);
        VerifyBasis(coordinates);
        Array.Reverse(coordinates);
        VerifyBasis(coordinates);
        if (count == 9) VerifyBasis(Enumerable.Range(247, 9).Select(value => (byte)value).ToArray());
    }

    [Fact]
    public void VariedSingleDigitCoordinateSetsStayWithinExactIntegerBounds()
    {
        var random = new Random(731255);
        byte[] pool = Enumerable.Range(1, 255).Select(value => (byte)value).ToArray();
        for (int count = 2; count <= 9; count++)
            for (int sample = 0; sample < 32; sample++)
            {
                for (int i = 0; i < count; i++)
                {
                    int other = random.Next(i, pool.Length);
                    (pool[i], pool[other]) = (pool[other], pool[i]);
                }
                VerifyBasis(pool[..count]);
            }
    }

    private static void VerifyBasis(byte[] coordinates)
    {
        int count = coordinates.Length;
        byte[] matrix = new byte[count * count * 32];
        SmallQuorumInterpolation.BuildBasis(coordinates, matrix);
        BigInteger modulus = PrimitiveTests.Order;
        for (int i = 0; i < count; i++)
        {
            BigInteger[] numerator = new BigInteger[count]; numerator[0] = 1;
            BigInteger denominator = 1;
            int degree = 0;
            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;
                var next = new BigInteger[count];
                for (int power = 0; power <= degree; power++)
                {
                    next[power] -= numerator[power] * coordinates[j];
                    next[power + 1] += numerator[power];
                }
                numerator = next;
                denominator *= coordinates[i] - coordinates[j];
                degree++;
            }
            denominator = (denominator % modulus + modulus) % modulus;
            BigInteger inverse = BigInteger.ModPow(denominator, modulus - 2, modulus);
            for (int power = 0; power < count; power++)
            {
                BigInteger expected = ((numerator[power] * inverse) % modulus + modulus) % modulus;
                Assert.Equal(expected, PrimitiveTests.Number(matrix.AsSpan((i * count + power) * 32, 32)));
            }
        }
        // L_i(x_j) = delta(i,j) checks the complete inverse Vandermonde matrix.
        for (int i = 0; i < count; i++) for (int j = 0; j < count; j++)
        {
            BigInteger value = 0;
            for (int power = count - 1; power >= 0; power--)
                value = (value * coordinates[j] + PrimitiveTests.Number(matrix.AsSpan((i * count + power) * 32, 32))) % modulus;
            Assert.Equal(i == j ? BigInteger.One : BigInteger.Zero, value);
        }
    }

    [Theory, MemberData(nameof(CoordinateCases))]
    public void OptimizedQuorumsMatchFrozenBaselineIncludingExtraShares(int count, bool sparse)
    {
        foreach (int length in new[] { 0, 8, 31, 32, 62, 63 })
        {
            foreach (int threshold in new[] { 2, count })
            {
                byte[] secret = Enumerable.Range(0, length).Select(i => (byte)((i * 31 + 19) & 255)).ToArray();
                var (all, commitments) = PedersenVssTests.Create(secret, threshold, sparse ? 255 : count);
                int shareLength = PedersenVss.GetShareLength(length);
                byte[] selected = new byte[count * shareLength];
                byte[] coordinates = Coordinates(count, sparse);
                for (int i = 0; i < count; i++) all.AsSpan((coordinates[i] - 1) * shareLength, shareLength).CopyTo(selected.AsSpan(i * shareLength));
                byte[] oldOutput = Enumerable.Repeat((byte)0xA5, length + 3).ToArray();
                byte[] newOutput = (byte[])oldOutput.Clone();
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(PedersenBaseline.VerifyShare(selected.AsSpan(i * shareLength, shareLength), commitments),
                        PedersenVss.VerifyShare(selected.AsSpan(i * shareLength, shareLength), commitments));
                }
                Assert.True(PedersenBaseline.TryReconstruct(selected, commitments, oldOutput, out int oldWritten));
                Assert.True(PedersenVss.TryReconstruct(selected, commitments, newOutput, out int newWritten));
                Assert.Equal(oldWritten, newWritten); Assert.Equal(oldOutput, newOutput); Assert.Equal(secret, newOutput[..length]);
                if (length != 0)
                {
                    // Canonical scalar tampering at every supplied position must
                    // fail, including extras and the final input share.
                    byte[] scratch = new byte[32];
                    byte[] one = PrimitiveTests.Encode(1);
                    for (int i = 0; i < count; i++)
                    {
                        int offset = i * shareLength + 33;
                        byte[] original = selected.AsSpan(offset, 32).ToArray();
                        Sodium.ScalarAdd(scratch, original, one); scratch.CopyTo(selected, offset);
                        Array.Fill(newOutput, (byte)0xA5);
                        Assert.False(PedersenVss.TryReconstruct(selected, commitments, newOutput, out int written));
                        Assert.Equal(0, written); Assert.All(newOutput, value => Assert.Equal(0xA5, value));
                        original.CopyTo(selected, offset);
                    }
                }
            }
        }
    }

    [Fact]
    public void SurplusCoefficientsAreCheckedAsGroupIdentityOpenings()
    {
        // Test the exact algebra with H=G (only in this internal lemma test).
        // Nonzero a2=7,b2=-7 open the identity and must be accepted. Production
        // always uses the fixed transparent H, with no caller-selected generator.
        byte[] h = new byte[32]; Ristretto.MultiplyBase(PrimitiveTests.Encode(1), h);
        byte[] a = [.. PrimitiveTests.Encode(5), .. PrimitiveTests.Encode(2), .. PrimitiveTests.Encode(7)];
        byte[] b = [.. PrimitiveTests.Encode(3), .. PrimitiveTests.Encode(4), .. PrimitiveTests.Encode(-7)];
        byte[] commitments = new byte[64];
        Ristretto.Commit(a.AsSpan(0, 32), b.AsSpan(0, 32), h, commitments.AsSpan(0, 32));
        Ristretto.Commit(a.AsSpan(32, 32), b.AsSpan(32, 32), h, commitments.AsSpan(32, 32));
        Assert.True(SmallQuorumInterpolation.VerifyCoefficients(a, b, commitments, h));
        b[64] ^= 1;
        Assert.False(SmallQuorumInterpolation.VerifyCoefficients(a, b, commitments, h));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void CorruptionWithAnUnchangedRecoveredConstantStillFailsVerification(int supplied)
    {
        var (shares, commitments) = PedersenVssTests.Create(new byte[32], 3, supplied);
        int length = PedersenVss.GetShareLength(32);
        byte[] changed = new byte[32];
        for (int i = 0; i < supplied; i++)
        {
            // Add X to f(X): f(0) is unchanged, but its X coefficient no longer
            // opens C1. Checking only the recovered constant would miss this.
            Sodium.ScalarAdd(changed, shares.AsSpan(i * length + 33, 32), PrimitiveTests.Encode(i + 1));
            changed.CopyTo(shares, i * length + 33);
        }
        byte[] output = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        Assert.False(PedersenBaseline.TryReconstruct(shares, commitments, output, out _));
        Assert.False(PedersenVss.TryReconstruct(shares, commitments, output, out int written));
        Assert.Equal(0, written); Assert.All(output, value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void DeterministicCreationIsByteIdenticalToTheOriginalProtocol()
    {
        foreach (int length in new[] { 0, 8, 31, 32, 62, 63 })
        {
            byte[] secret = Enumerable.Repeat((byte)0xA5, length).ToArray();
            byte[] oldShares = new byte[PedersenVss.GetSharesLength(length, 5)], newShares = new byte[oldShares.Length];
            byte[] oldCommitments = new byte[PedersenVss.GetCommitmentsLength(length, 3)], newCommitments = new byte[oldCommitments.Length];
            PedersenBaseline.CreateShares(secret, 3, 5, oldShares, oldCommitments, new CountingRandom());
            PedersenVss.CreateShares(secret, 3, 5, newShares, newCommitments, new CountingRandom());
            Assert.Equal(oldShares, newShares); Assert.Equal(oldCommitments, newCommitments);
        }
    }

    [Theory]
    [InlineData(62, 9)]
    [InlineData(63, 9)]
    [InlineData(32, 10)]
    [InlineData(32, 16)]
    public void FastPathBoundariesAndFallbacksMatchBaseline(int length, int count)
    {
        var (shares, commitments) = PedersenVssTests.Create(new byte[length], count, count);
        byte[] expected = new byte[length], actual = new byte[length];
        Assert.True(PedersenBaseline.TryReconstruct(shares, commitments, expected, out _));
        Assert.True(PedersenVss.TryReconstruct(shares, commitments, actual, out _));
        Assert.Equal(expected, actual);
    }

    [Theory, MemberData(nameof(CoordinateCases))]
    public void OptimizedSuccessAndCanonicalFailurePathsAllocateZeroBytes(int count, bool sparse)
    {
        byte[] secret = new byte[32];
        var (all, commitments) = PedersenVssTests.Create(secret, count, sparse ? 255 : count);
        int length = PedersenVss.GetShareLength(32);
        byte[] selected = new byte[count * length], output = new byte[32];
        byte[] coordinates = Coordinates(count, sparse);
        for (int i = 0; i < count; i++) all.AsSpan((coordinates[i] - 1) * length, length).CopyTo(selected.AsSpan(i * length));
        PedersenVss.TryReconstruct(selected, commitments, output, out _);
        PedersenVss.VerifyShare(selected.AsSpan(0, length), commitments);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool reconstructed = PedersenVss.TryReconstruct(selected, commitments, output, out _);
        bool valid = PedersenVss.VerifyShare(selected.AsSpan(0, length), commitments);
        long successBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        byte[] changed = new byte[32];
        Sodium.ScalarAdd(changed, selected.AsSpan(33, 32), PrimitiveTests.Encode(1));
        changed.CopyTo(selected, 33);
        before = GC.GetAllocatedBytesForCurrentThread();
        bool rejected = !PedersenVss.TryReconstruct(selected, commitments, output, out _);
        long failureBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(reconstructed); Assert.True(valid); Assert.True(rejected);
        Assert.Equal(0L, successBytes); Assert.Equal(0L, failureBytes);
    }

    private static byte[] Coordinates(int count, bool sparse) => Enumerable.Range(0, count)
        .Select(i => sparse ? (byte)((254 + 73 * i) % 255 + 1) : (byte)(i + 1)).ToArray();

    private sealed class CountingRandom : Mrbr.Cryptography.Vss.Internal.IRandomSource, Mrbr.Cryptography.Vss.OptimizationBenchmarks.Baselines.IRandomSource
    {
        private int calls;
        public void Fill(Span<byte> destination) { destination.Clear(); destination[0] = (byte)(++calls & 255); }
    }
}
