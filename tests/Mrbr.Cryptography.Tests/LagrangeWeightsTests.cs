using Mrbr.Cryptography.Internal;
using Mrbr.Cryptography.WeightBenchmarks.Baselines;
using Mrbr.Cryptography.WeightMeasurements;

namespace Mrbr.Cryptography.Tests;

public sealed class LagrangeWeightsTests
{
    public static IEnumerable<object[]> SmallCases()
    {
        for (int count = 2; count <= 9; count++)
            foreach (bool sparse in new[] { false, true }) yield return [count, sparse];
    }

    public static IEnumerable<object[]> CoordinateCases()
    {
        foreach (object[] testCase in SmallCases()) yield return testCase;
        foreach (int count in new[] { 10, 16, 32, 64, 128, 192, 254, 255 })
            foreach (bool sparse in new[] { false, true }) yield return [count, sparse];
    }

    [Fact]
    public void EveryOrderedPairOfDistinctNonzeroCoordinatesMatchesIndependentDivision()
    {
        Span<byte> coordinates = stackalloc byte[2];
        Span<byte> weights = stackalloc byte[2];
        for (int first = 1; first <= 255; first++)
        {
            for (int second = 1; second <= 255; second++)
            {
                if (first == second) continue;
                coordinates[0] = (byte)first;
                coordinates[1] = (byte)second;
                LagrangeWeights.Calculate(coordinates, weights);
                Assert.Equal(Reference.Multiply((byte)second, Reference.Inverses[first ^ second]), weights[0]);
                Assert.Equal(Reference.Multiply((byte)first, Reference.Inverses[first ^ second]), weights[1]);
            }
        }
    }

    [Theory, MemberData(nameof(CoordinateCases))]
    public void WeightsMatchIndependentOracleAndFollowCoordinatePermutations(int count, bool sparse)
    {
        byte[] coordinates = WeightScenarios.Coordinates(count, sparse);
        byte[] original = (byte[])coordinates.Clone();
        byte[] expected = Reference.Weights(coordinates);
        byte[] weights = Enumerable.Repeat((byte)0xA5, count + 3).ToArray();
        LagrangeWeights.Calculate(coordinates, weights);
        Assert.Equal(expected, weights[..count]);
        Assert.Equal(original, coordinates);
        Assert.All(weights[count..], value => Assert.Equal(0xA5, value));
        Assert.All(weights[..count], value => Assert.NotEqual(0, value));

        Array.Reverse(coordinates);
        LagrangeWeights.Calculate(coordinates, weights);
        Assert.Equal(expected.Reverse().ToArray(), weights[..count]);
        byte first = coordinates[0];
        coordinates.AsSpan(1).CopyTo(coordinates);
        coordinates[^1] = first;
        LagrangeWeights.Calculate(coordinates, weights);
        Assert.Equal(Reference.Weights(coordinates), weights[..count]);

        // Interpolating the monomials 1,x,...,x^(m-1) at zero must give
        // 1,0,...,0. This independently checks the basis, including sum(w)=1.
        int degrees = count <= 9 ? count : 2;
        for (int degree = 0; degree < degrees; degree++)
        {
            byte sum = 0;
            for (int i = 0; i < count; i++)
            {
                byte power = 1;
                for (int j = 0; j < degree; j++) power = Reference.Multiply(power, coordinates[i]);
                sum ^= Reference.Multiply(weights[i], power);
            }
            Assert.Equal(degree == 0 ? (byte)1 : (byte)0, sum);
        }
    }

    [Fact]
    public void ThousandsOfDifferentSingleDigitCoordinateSetsMatchIndependentWeights()
    {
        var random = new Random(0x11B03);
        byte[] pool = Enumerable.Range(1, 255).Select(value => (byte)value).ToArray();
        Span<byte> weights = stackalloc byte[9];
        for (int count = 2; count <= 9; count++)
        {
            for (int sample = 0; sample < 512; sample++)
            {
                for (int i = 0; i < count; i++)
                {
                    int other = random.Next(i, pool.Length);
                    (pool[i], pool[other]) = (pool[other], pool[i]);
                }
                ReadOnlySpan<byte> coordinates = pool.AsSpan(0, count);
                LagrangeWeights.Calculate(coordinates, weights);
                Assert.True(weights[..count].SequenceEqual(Reference.Weights(coordinates)));
            }
        }
    }

    [Theory, MemberData(nameof(CoordinateCases))]
    public void PublicReconstructionMatchesIndependentInterpolationAndFrozenSimdBaseline(int count, bool sparse)
    {
        foreach (int length in new[] { 0, 1, 8, 15, 16, 24, 31, 32, 33, 65 })
        {
            byte[] packed = WeightScenarios.Shares(length, count, sparse);
            byte[] original = (byte[])packed.Clone();
            byte[] coordinates = WeightScenarios.Coordinates(count, sparse);
            byte[] weights = Reference.Weights(coordinates);
            byte[] expected = new byte[length];
            for (int i = 0; i < count; i++)
                for (int j = 0; j < length; j++)
                    expected[j] ^= Reference.Multiply(weights[i], packed[i * (length + 1) + 1 + j]);
            byte[] destination = Enumerable.Repeat((byte)0xA5, length + 5).ToArray();
            byte[] baseline = new byte[length];
            // Both metadata thresholds must use all supplied shares, including
            // arbitrary payloads that are not authenticated as a common group.
            foreach (int threshold in new[] { 2, count })
            {
                Assert.Equal(length, ShamirSharedSecret.Reconstruct(packed, length + 1, threshold, destination.AsSpan(1, length + 3)));
                Assert.Equal(length, SimdBaseline.Reconstruct(packed, length + 1, threshold, baseline));
                Assert.Equal(expected, destination[1..(length + 1)]);
                Assert.Equal(expected, baseline);
                Assert.Equal(0xA5, destination[0]);
                Assert.All(destination[(length + 1)..], value => Assert.Equal(0xA5, value));
            }
            Assert.Equal(original, packed);
        }
    }

    [Theory, MemberData(nameof(SmallCases))]
    public void PublicReconstructionAndWeightCalculationAllocateZeroManagedBytes(int count, bool sparse)
    {
        byte[] coordinates = WeightScenarios.Coordinates(count, sparse);
        byte[] weights = new byte[count];
        foreach (int length in new[] { 8, 16, 24, 32 })
        {
            byte[] packed = WeightScenarios.Shares(length, count, sparse);
            byte[] destination = new byte[length];
            ShamirSharedSecret.Reconstruct(packed, length + 1, count, destination);
            LagrangeWeights.Calculate(coordinates, weights);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) ShamirSharedSecret.Reconstruct(packed, length + 1, count, destination);
            long reconstructionBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) LagrangeWeights.Calculate(coordinates, weights);
            long weightBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, reconstructionBytes);
            Assert.Equal(0L, weightBytes);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void FastCasesStillRejectInvalidCoordinatesBeforeWriting(int count)
    {
        byte[] shares = WeightScenarios.Shares(8, count, false);
        byte[] destination = Enumerable.Repeat((byte)0xA5, 8).ToArray();
        shares[0] = 0;
        Assert.Throws<ArgumentException>(() => ShamirSharedSecret.Reconstruct(shares, 9, count, destination));
        shares[0] = 1;
        shares[9] = 1;
        Assert.Throws<ArgumentException>(() => ShamirSharedSecret.Reconstruct(shares, 9, count, destination));
        Assert.All(destination, value => Assert.Equal(0xA5, value));
    }

    // Independent polynomial long division and exhaustive inverse search.
    // This oracle uses neither production field arithmetic nor log tables.
    private static class Reference
    {
        internal static readonly byte[] Inverses = BuildInverses();

        internal static byte Multiply(byte left, byte right)
        {
            int polynomial = 0;
            for (int bit = 0; bit < 8; bit++)
                if ((right & (1 << bit)) != 0) polynomial ^= left << bit;
            for (int degree = 14; degree >= 8; degree--)
                if ((polynomial & (1 << degree)) != 0) polynomial ^= 0x11B << (degree - 8);
            return (byte)polynomial;
        }

        internal static byte[] Weights(ReadOnlySpan<byte> coordinates)
        {
            byte[] weights = new byte[coordinates.Length];
            for (int i = 0; i < coordinates.Length; i++)
            {
                byte numerator = 1, denominator = 1;
                for (int j = 0; j < coordinates.Length; j++)
                {
                    if (i == j) continue;
                    numerator = Multiply(numerator, coordinates[j]);
                    denominator = Multiply(denominator, (byte)(coordinates[i] ^ coordinates[j]));
                }
                weights[i] = Multiply(numerator, Inverses[denominator]);
            }
            return weights;
        }

        private static byte[] BuildInverses()
        {
            byte[] inverses = new byte[256];
            for (int value = 1; value <= 255; value++)
                for (int candidate = 1; candidate <= 255; candidate++)
                    if (Multiply((byte)value, (byte)candidate) == 1)
                    {
                        inverses[value] = (byte)candidate;
                        break;
                    }
            return inverses;
        }
    }
}
