using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Mrbr.Cryptography.Vss.Internal;

namespace Mrbr.Cryptography.Vss.Tests;

public sealed class PrimitiveTests
{
    internal static BigInteger Order => (BigInteger.One << 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    internal static BigInteger Number(ReadOnlySpan<byte> bytes) => new(bytes, isUnsigned: true, isBigEndian: false);
    internal static byte[] Encode(BigInteger value)
    {
        byte[] output = new byte[32];
        ((value % Order + Order) % Order).ToByteArray(isUnsigned: true).CopyTo(output, 0);
        return output;
    }

    [Fact]
    public unsafe void NativeBackendVersionAndHashFunctionsMatchPinnedPackageAndBcl()
    {
        Assert.Equal("1.0.22", Marshal.PtrToStringUTF8((IntPtr)Sodium.Version()));
        byte[] data = RandomNumberGenerator.GetBytes(1537);
        byte[] hash = new byte[64];
        Sodium.Hash256(hash.AsSpan(0, 32), data);
        Assert.Equal(SHA256.HashData(data), hash[..32]);
        Sodium.Hash512(hash, data);
        Assert.Equal(SHA512.HashData(data), hash);
    }

    [Theory]
    [InlineData(0, "0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData(1, "e2f2ae0a6abc4e71a884a961c500515f58e30b6aa582dd8db6a65945e08d2d76")]
    [InlineData(2, "6a493210f7499cd17fecb510ae0cea23a110e8d5b901f8acadd3095c73a3b919")]
    [InlineData(3, "94741f5d5d52755ece4f23f044ee27d5d1ea1e2bd196b462166b16152a9d0259")]
    public void BaseMultiplicationMatchesRfc9496IncludingZero(int scalar, string point)
    {
        byte[] result = new byte[32];
        Ristretto.MultiplyBase(Encode(scalar), result);
        Assert.Equal(Convert.FromHexString(point), result);
        Assert.True(Sodium.PointIsValid(result));
    }

    [Fact]
    public void HashToGroupMatchesRfc9496AndTransparentHDerivation()
    {
        byte[] input = Convert.FromHexString("5d1be09e3d0c82fc538112490e35701979d99e06ca3e2b5b54bffe8b4dc772c14d98b696a1bbfb5ca32c436cc61c16563790306c79eaca7705668b47dffe5bb6");
        byte[] result = new byte[32];
        Sodium.PointFromHash(result, input);
        Assert.Equal(Convert.FromHexString("3066f82a1a747d45120d1740f14358531a8f04bbffe6a819f86dfe50f44a0a46"), result);
        byte[] expected = new byte[32];
        Sodium.PointFromHash(expected, SHA512.HashData("Mrbr.Cryptography.PedersenVss/v1/H/ristretto255"u8));
        Ristretto.DeriveH(result);
        Assert.Equal(expected, result);
        Assert.True(Sodium.PointIsValid(result));
        Assert.Contains(result, value => value != 0);
    }

    [Fact]
    public void ScalarArithmeticAndCanonicalityMatchIndependentBigIntegerOracle()
    {
        Assert.Equal(Order, Number(Ristretto.Order));
        Assert.True(Ristretto.IsCanonicalScalar(Encode(0)));
        Assert.True(Ristretto.IsCanonicalScalar(Encode(Order - 1)));
        Assert.False(Ristretto.IsCanonicalScalar(Ristretto.Order));
        Assert.False(Ristretto.IsCanonicalScalar(Enumerable.Repeat((byte)255, 32).ToArray()));
        Assert.False(Ristretto.IsCanonicalScalar(new byte[31]));
        var values = new List<BigInteger> { 0, 1, 2, Order - 1, Order / 2, (BigInteger.One << 248) - 1 };
        for (int i = 0; i < 24; i++) values.Add(Number(RandomNumberGenerator.GetBytes(32)) % Order);
        byte[] result = new byte[32];
        foreach (BigInteger left in values)
        {
            byte[] a = Encode(left);
            Assert.True(Ristretto.IsCanonicalScalar(a));
            if (left != 0)
            {
                Sodium.ScalarInverse(result, a);
                Assert.Equal(BigInteger.ModPow(left, Order - 2, Order), Number(result));
            }
            foreach (BigInteger right in values)
            {
                byte[] b = Encode(right);
                Sodium.ScalarAdd(result, a, b);
                Assert.Equal((left + right) % Order, Number(result));
                Sodium.ScalarSubtract(result, a, b);
                Assert.Equal((left - right + Order) % Order, Number(result));
                Sodium.ScalarMultiply(result, a, b);
                Assert.Equal(left * right % Order, Number(result));
            }
        }
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    [InlineData(9, false)]
    [InlineData(9, true)]
    [InlineData(255, true)]
    public void PrimeFieldWeightsMatchIndependentInterpolation(int count, bool sparse)
    {
        byte[] coordinates = Enumerable.Range(0, count).Select(i => sparse ? (byte)((254 + i * 73) % 255 + 1) : (byte)(i + 1)).ToArray();
        byte[] weights = new byte[count * 32];
        Ristretto.Weights(coordinates, weights);
        BigInteger sum = 0;
        for (int i = 0; i < count; i++)
        {
            BigInteger numerator = 1, denominator = 1;
            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;
                numerator = numerator * coordinates[j] % Order;
                denominator = denominator * (coordinates[j] - coordinates[i] + Order) % Order;
            }
            BigInteger expected = numerator * BigInteger.ModPow(denominator, Order - 2, Order) % Order;
            Assert.Equal(expected, Number(weights.AsSpan(i * 32, 32)));
            sum = (sum + expected) % Order;
        }
        Assert.Equal(BigInteger.One, sum);
    }

    [Fact]
    public void RejectionSamplerAcceptsZeroAndRejectsTheOrderItself()
    {
        byte[] result = new byte[32];
        var random = new RejectThenZeroRandom();
        Ristretto.RandomScalar(result, random);
        Assert.Equal(2, random.Calls);
        Assert.All(result, value => Assert.Equal(0, value));
        byte[] h = new byte[32], point = new byte[32];
        Ristretto.DeriveH(h);
        Ristretto.MultiplyPublicPoint(result, h, point);
        Assert.All(point, value => Assert.Equal(0, value));
        Ristretto.MultiplyPublicPoint(Encode(3), new byte[32], point);
        Assert.All(point, value => Assert.Equal(0, value));
    }

    private sealed class RejectThenZeroRandom : IRandomSource
    {
        internal int Calls;
        public void Fill(Span<byte> destination)
        {
            if (++Calls == 1) Ristretto.Order.CopyTo(destination);
            else destination.Clear();
        }
    }
}
