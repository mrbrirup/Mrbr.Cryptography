using System.Security.Cryptography;
using Mrbr.Cryptography.Internal;

namespace Mrbr.Cryptography.Tests;

public sealed class ShamirSharedSecretTests
{
    // secret = [42, 00, FF]; per-byte [a1, a2] = [11, 7E], [00, A5], [FF, 00].
    // Expected values were calculated by carry-less multiplication and long division,
    // independently of production Horner evaluation and reconstruction.
    private static byte[] KnownShares =>
    [
        0x01, 0x2D, 0xA5, 0x00,
        0x02, 0x83, 0xA2, 0x1A,
        0x03, 0xEC, 0x07, 0xE5,
        0x04, 0xA7, 0xBE, 0x2E,
        0x05, 0xC8, 0x1B, 0xD1,
    ];

    [Theory]
    [InlineData(0, 2, 1, 2)]
    [InlineData(1, 5, 2, 10)]
    [InlineData(32, 5, 33, 165)]
    [InlineData(4096, 255, 4097, 1044735)]
    public void SizeHelpers_IncludeOneCoordinateBytePerShare(int secretLength, int count, int shareLength, int totalLength)
    {
        Assert.Equal(shareLength, ShamirSharedSecret.GetShareLength(secretLength));
        Assert.Equal(totalLength, ShamirSharedSecret.GetSharesLength(secretLength, count));
    }

    [Fact]
    public void CreateShares_MatchesFixedVector_IncludingZeroCoefficients()
    {
        byte[] secret = [0x42, 0x00, 0xFF];
        byte[] original = (byte[])secret.Clone();
        byte[] destination = new byte[25];
        destination.AsSpan().Fill(0xCC);
        var source = new FixedRandomSource([0x11, 0x7E, 0x00, 0xA5, 0xFF, 0x00]);

        int written = ShamirSharedSecret.CreateShares(secret, 3, 5, destination, source);

        Assert.Equal(20, written);
        Assert.Equal(KnownShares, destination[..written]);
        Assert.Equal(original, secret);
        Assert.All(destination[written..], value => Assert.Equal((byte)0xCC, value));
        Assert.Equal(6, source.BytesConsumed);
    }

    [Fact]
    public void Reconstruct_EveryThresholdSubsetAndPermutation_MatchesFixedVector()
    {
        byte[] shares = KnownShares;
        byte[] original = (byte[])shares.Clone();
        byte[] expected = [0x42, 0x00, 0xFF];
        int[][] permutations = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];

        for (int first = 0; first < 3; first++)
        {
            for (int second = first + 1; second < 4; second++)
            {
                for (int third = second + 1; third < 5; third++)
                {
                    int[] indices = [first, second, third];
                    foreach (int[] permutation in permutations)
                    {
                        byte[] selected = new byte[12];
                        for (int index = 0; index < 3; index++)
                        {
                            shares.AsSpan(indices[permutation[index]] * 4, 4).CopyTo(selected.AsSpan(index * 4));
                        }

                        byte[] selectedOriginal = (byte[])selected.Clone();
                        byte[] destination = [0xCC, 0xCC, 0xCC, 0xCC, 0xCC];

                        int written = ShamirSharedSecret.Reconstruct(selected, 4, 3, destination);

                        Assert.Equal(3, written);
                        Assert.Equal(expected, destination[..written]);
                        Assert.Equal((byte)0xCC, destination[3]);
                        Assert.Equal((byte)0xCC, destination[4]);
                        Assert.Equal(selectedOriginal, selected);
                    }
                }
            }
        }

        Assert.Equal(original, shares);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Reconstruct_AcceptsMoreThanThresholdShares(int count)
    {
        byte[] destination = new byte[3];

        int written = ShamirSharedSecret.Reconstruct(KnownShares.AsSpan(0, count * 4), 4, 3, destination);

        Assert.Equal(3, written);
        Assert.Equal(new byte[] { 0x42, 0x00, 0xFF }, destination);
    }

    [Fact]
    public void Reconstruct_UsesAllShares_AndDoesNotAuthenticatePayloads()
    {
        byte[] shares = KnownShares;
        shares[17] ^= 1; // Change the last share's first payload byte, keeping its coordinate valid.
        byte[] destination = new byte[3];

        ShamirSharedSecret.Reconstruct(shares, 4, 3, destination);

        Assert.NotEqual((byte)0x42, destination[0]);
        Assert.Equal((byte)0x00, destination[1]);
        Assert.Equal((byte)0xFF, destination[2]);
    }

    [Fact]
    public void Reconstruct_IncorrectSuppliedThreshold_CannotReliablyBeDetected()
    {
        byte[] destination = new byte[3];

        // The API knows only the caller's threshold; these were created with k=3.
        ShamirSharedSecret.Reconstruct(KnownShares.AsSpan(0, 8), 4, 2, destination);

        Assert.NotEqual((byte)0x42, destination[0]);
    }

    [Theory]
    [InlineData(0, 2, 2)]
    [InlineData(1, 2, 5)]
    [InlineData(256, 3, 5)]
    [InlineData(4097, 2, 3)]
    [InlineData(2049, 3, 5)]
    [InlineData(33, 255, 255)]
    [InlineData(31, 17, 31)]
    public void PublicApis_RoundTripBinaryData_WithReorderedThresholdSubset(int length, int threshold, int count)
    {
        byte[] secret = new byte[length];
        for (int index = 0; index < secret.Length; index++)
        {
            secret[index] = (byte)(index & 0xFF);
        }

        byte[] original = (byte[])secret.Clone();
        int shareLength = ShamirSharedSecret.GetShareLength(length);
        byte[] shares = new byte[ShamirSharedSecret.GetSharesLength(length, count)];

        Assert.Equal(shares.Length, ShamirSharedSecret.CreateShares(secret, threshold, count, shares));
        for (int share = 0; share < count; share++)
        {
            Assert.Equal((byte)(share + 1), shares[share * shareLength]);
        }

        byte[] selected = new byte[threshold * shareLength];
        for (int index = 0; index < threshold; index++)
        {
            // Alternate between the highest and lowest coordinates.
            int share = (index & 1) == 0 ? count - 1 - (index / 2) : index / 2;
            shares.AsSpan(share * shareLength, shareLength).CopyTo(selected.AsSpan(index * shareLength));
        }

        byte[] destination = new byte[length];
        Assert.Equal(length, ShamirSharedSecret.Reconstruct(selected, shareLength, threshold, destination));
        Assert.Equal(original, destination);
        Assert.Equal(original, secret);
    }

    [Theory]
    [InlineData(2, 4097)]
    [InlineData(3, 2051)]
    [InlineData(255, 17)]
    public void CreateShares_ConsumesFreshCoefficientsAcrossBatchBoundaries(int threshold, int length)
    {
        int count = threshold;
        int degree = threshold - 1;
        byte[] secret = new byte[length];
        secret.AsSpan().Fill(0x42);
        int shareLength = ShamirSharedSecret.GetShareLength(length);
        byte[] shares = new byte[ShamirSharedSecret.GetSharesLength(length, count)];
        var source = new CountingRandomSource();

        ShamirSharedSecret.CreateShares(secret, threshold, count, shares, source);

        Assert.Equal(length * degree, source.BytesConsumed);
        Assert.Equal((length + (4096 / degree) - 1) / (4096 / degree), source.CallCount);
        byte[] coefficients = new byte[degree];
        for (int position = 0; position < length; position++)
        {
            for (int coefficient = 0; coefficient < degree; coefficient++)
            {
                coefficients[coefficient] = (byte)(((position * degree) + coefficient) & 0xFF);
            }

            // Check low and high coordinates using direct powers, not Horner's rule.
            foreach (int share in new[] { 0, count - 1 })
            {
                byte expected = ReferenceEvaluate(secret[position], coefficients, (byte)(share + 1));
                Assert.Equal(expected, shares[(share * shareLength) + 1 + position]);
            }
        }
    }

    [Fact]
    public void HornerEvaluation_MatchesIndependentDirectPowers_ForEveryCoordinate()
    {
        byte[] highDegree = new byte[254];
        for (int index = 0; index < highDegree.Length; index++)
        {
            highDegree[index] = (byte)((index * 37) & 0xFF);
        }

        byte[][] polynomials = [[], [0], [0x11, 0x7E], [0xFF, 0, 0x81, 0, 2], highDegree];
        foreach (byte[] coefficients in polynomials)
        {
            for (int coordinate = 0; coordinate < 256; coordinate++)
            {
                byte constant = (byte)((coordinate * 19) & 0xFF);
                byte expected = ReferenceEvaluate(constant, coefficients, (byte)coordinate);

                Assert.Equal(expected, ShamirSharedSecret.EvaluatePolynomial(constant, coefficients, (byte)coordinate));
            }
        }
    }

    [Fact]
    public void OnePointInATwoShareScheme_IsCompatibleWithEveryPossibleSecret()
    {
        const byte coordinate = 7;
        const byte payload = 0xA6;
        byte inverse = ReferenceInverse(coordinate);

        for (int secret = 0; secret < 256; secret++)
        {
            // For every candidate secret, a1 = (payload + secret) / coordinate
            // gives a polynomial passing through the exact same observed point.
            byte coefficient = ReferenceMultiply((byte)(payload ^ secret), inverse);

            Assert.Equal(payload, ShamirSharedSecret.EvaluatePolynomial((byte)secret, [coefficient], coordinate));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CreateShares_RandomFailure_ClearsPartialOutputAndPreservesSuffix(int successfulCalls)
    {
        byte[] secret = new byte[4097];
        secret.AsSpan().Fill(0x42);
        int required = ShamirSharedSecret.GetSharesLength(secret.Length, 2);
        byte[] destination = new byte[required + 7];
        destination.AsSpan().Fill(0xCC);
        var source = new FailingRandomSource(successfulCalls);

        Assert.Throws<CryptographicException>(() => ShamirSharedSecret.CreateShares(secret, 2, 2, destination, source));

        Assert.Equal(successfulCalls + 1, source.CallCount);
        Assert.All(destination[..required], value => Assert.Equal((byte)0, value));
        Assert.All(destination[required..], value => Assert.Equal((byte)0xCC, value));
        Assert.All(secret, value => Assert.Equal((byte)0x42, value));
    }

    [Fact]
    public void CreateShares_EmptySecret_WritesOnlyCoordinatesWithoutRequestingEntropy()
    {
        byte[] destination = [0xCC, 0xCC, 0xCC];
        var source = new FailingRandomSource(0);

        Assert.Equal(2, ShamirSharedSecret.CreateShares([], 2, 2, destination, source));
        Assert.Equal(new byte[] { 1, 2, 0xCC }, destination);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void CreateShares_AllZeroCoefficients_AreAcceptedWithoutResampling()
    {
        byte[] secret = [0x00, 0x42, 0xFF];
        byte[] destination = new byte[20];
        var source = new FixedRandomSource(new byte[6]);

        ShamirSharedSecret.CreateShares(secret, 3, 5, destination, source);

        for (int share = 0; share < 5; share++)
        {
            Assert.Equal(secret, destination[((share * 4) + 1)..((share + 1) * 4)]);
        }

        Assert.Equal(6, source.BytesConsumed);
    }

    [Fact]
    public void PublicApis_AcceptDisjointSlicesOfTheSameBuffer_AndPreserveSurroundingBytes()
    {
        byte[] buffer = new byte[40];
        buffer.AsSpan().Fill(0xCC);
        ReadOnlySpan<byte> expected = [0x42, 0x00, 0xFF];
        Span<byte> secret = buffer.AsSpan(1, 3);
        Span<byte> shares = buffer.AsSpan(8, 20);
        Span<byte> destination = buffer.AsSpan(32, 3);
        expected.CopyTo(secret);

        Assert.Equal(20, ShamirSharedSecret.CreateShares(secret, 3, 5, shares));
        Assert.Equal(3, ShamirSharedSecret.Reconstruct(shares, 4, 3, destination));

        Assert.True(expected.SequenceEqual(secret));
        Assert.True(expected.SequenceEqual(destination));
        for (int index = 0; index < buffer.Length; index++)
        {
            if (index is not (>= 1 and < 4) and not (>= 8 and < 28) and not (>= 32 and < 35))
            {
                Assert.Equal((byte)0xCC, buffer[index]);
            }
        }
    }

    private static byte ReferenceEvaluate(byte constant, ReadOnlySpan<byte> coefficients, byte coordinate)
    {
        byte value = constant;
        byte power = coordinate;
        foreach (byte coefficient in coefficients)
        {
            value ^= ReferenceMultiply(coefficient, power);
            power = ReferenceMultiply(power, coordinate);
        }

        return value;
    }

    private static byte ReferenceMultiply(byte left, byte right)
    {
        int polynomial = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            if ((right & (1 << bit)) != 0)
            {
                polynomial ^= left << bit;
            }
        }

        for (int degree = 14; degree >= 8; degree--)
        {
            if ((polynomial & (1 << degree)) != 0)
            {
                polynomial ^= 0x11B << (degree - 8);
            }
        }

        return (byte)polynomial;
    }

    private static byte ReferenceInverse(byte value)
    {
        for (int candidate = 1; candidate < 256; candidate++)
        {
            if (ReferenceMultiply(value, (byte)candidate) == 1)
            {
                return (byte)candidate;
            }
        }

        throw new InvalidOperationException("No inverse exists.");
    }

    private sealed class FixedRandomSource(byte[] coefficients) : IRandomSource
    {
        public int BytesConsumed { get; private set; }

        public void Fill(Span<byte> destination)
        {
            coefficients.AsSpan(BytesConsumed, destination.Length).CopyTo(destination);
            BytesConsumed += destination.Length;
        }
    }

    internal sealed class CountingRandomSource : IRandomSource
    {
        public int BytesConsumed { get; private set; }
        public int CallCount { get; private set; }

        public void Fill(Span<byte> destination)
        {
            CallCount++;
            for (int index = 0; index < destination.Length; index++)
            {
                destination[index] = (byte)((BytesConsumed + index) & 0xFF);
            }

            BytesConsumed += destination.Length;
        }
    }

    private sealed class FailingRandomSource(int successfulCalls) : IRandomSource
    {
        public int CallCount { get; private set; }

        public void Fill(Span<byte> destination)
        {
            CallCount++;
            destination.Fill(0xA5); // Also exercise failure after a partial RNG write.
            if (CallCount > successfulCalls)
            {
                throw new CryptographicException("Simulated entropy failure.");
            }
        }
    }
}
