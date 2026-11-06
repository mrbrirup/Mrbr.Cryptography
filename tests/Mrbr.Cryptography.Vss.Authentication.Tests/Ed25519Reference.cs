using System.Numerics;
using System.Security.Cryptography;

namespace Mrbr.Cryptography.Vss.Authentication.Tests;

// Test-only, allocating, variable-time BigInteger oracle for RFC 8032 section 5.1.
// Shares no curve/scalar/hash code with the production native binding.
internal static class Ed25519Reference
{
    private static readonly BigInteger P = (BigInteger.One << 255) - 19;
    internal static readonly BigInteger Order = (BigInteger.One << 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    private static readonly BigInteger D = Mod(-121665 * BigInteger.ModPow(121666, P - 2, P));
    private static readonly Point Base = FromAffine(
        BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202"),
        BigInteger.Parse("46316835694926478169428394003475163141307993866256225615783033603165251855960"));

    internal static byte[] PublicKey(byte[] seed) => Encode(Multiply(Expand(seed).Scalar));

    internal static byte[] Sign(byte[] seed, byte[] message)
    {
        var (scalar, prefix) = Expand(seed);
        byte[] publicKey = Encode(Multiply(scalar));
        BigInteger nonce = Number(SHA512.HashData(Concat(prefix, message))) % Order;
        byte[] r = Encode(Multiply(nonce));
        BigInteger challenge = Number(SHA512.HashData(Concat(r, publicKey, message))) % Order;
        return Concat(r, Bytes((nonce + challenge * scalar) % Order));
    }

    private static (BigInteger Scalar, byte[] Prefix) Expand(byte[] seed)
    {
        byte[] expanded = SHA512.HashData(seed);
        expanded[0] &= 248; expanded[31] &= 63; expanded[31] |= 64;
        return (Number(expanded[..32]), expanded[32..]);
    }

    private readonly record struct Point(BigInteger X, BigInteger Y, BigInteger Z, BigInteger T);
    private static Point FromAffine(BigInteger x, BigInteger y) => new(x, y, 1, Mod(x * y));
    private static BigInteger Mod(BigInteger value) => (value % P + P) % P;
    private static Point Add(Point left, Point right)
    {
        BigInteger a = Mod((left.Y - left.X) * (right.Y - right.X));
        BigInteger b = Mod((left.Y + left.X) * (right.Y + right.X));
        BigInteger c = Mod(2 * D * left.T * right.T), d = Mod(2 * left.Z * right.Z);
        BigInteger e = b - a, f = d - c, g = d + c, h = b + a;
        return new(Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h));
    }
    private static Point Multiply(BigInteger scalar)
    {
        Point result = FromAffine(0, 1), point = Base;
        while (scalar > 0)
        {
            if (!scalar.IsEven) result = Add(result, point);
            point = Add(point, point); scalar >>= 1;
        }
        return result;
    }
    private static byte[] Encode(Point point)
    {
        BigInteger inverse = BigInteger.ModPow(point.Z, P - 2, P);
        BigInteger x = Mod(point.X * inverse), y = Mod(point.Y * inverse);
        return Bytes(y | ((x & 1) << 255));
    }
    internal static BigInteger Number(byte[] value) => new(value, isUnsigned: true, isBigEndian: false);
    internal static byte[] Bytes(BigInteger value)
    {
        byte[] result = new byte[32];
        if (!value.TryWriteBytes(result, out _, isUnsigned: true, isBigEndian: false)) throw new InvalidOperationException();
        return result;
    }
    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();
}
