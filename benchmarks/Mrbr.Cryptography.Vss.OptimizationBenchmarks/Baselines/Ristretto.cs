// Frozen VSS increment baseline; only namespace/type names changed.
using System.Security.Cryptography;

namespace Mrbr.Cryptography.Vss.OptimizationBenchmarks.Baselines;

internal interface IRandomSource { void Fill(Span<byte> destination); }
internal readonly struct SystemRandomSource : IRandomSource
{
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}

internal static class RistrettoBaseline
{
    // l = 2^252 + 27742317777372353535851937790883648493, little endian.
    internal static ReadOnlySpan<byte> Order =>
    [
        0xED, 0xD3, 0xF5, 0x5C, 0x1A, 0x63, 0x12, 0x58,
        0xD6, 0x9C, 0xF7, 0xA2, 0xDE, 0xF9, 0xDE, 0x14,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x10
    ];

    internal static bool IsCanonicalScalar(ReadOnlySpan<byte> scalar)
    {
        if (scalar.Length != 32) return false;
        int borrow = 0;
        ReadOnlySpan<byte> order = Order;
        for (int i = 0; i < 32; i++) borrow = (scalar[i] - order[i] - borrow) >> 31 & 1;
        return borrow == 1;
    }

    internal static void RandomScalar<T>(Span<byte> scalar, T random) where T : IRandomSource
    {
        // Exact uniform sampling in [0,l), including zero. Reducing random bytes
        // modulo l or forbidding zero would weaken the exact hiding argument.
        do
        {
            random.Fill(scalar);
            scalar[31] &= 0x1F;
        } while (!IsCanonicalScalar(scalar));
    }

    internal static void DeriveH(Span<byte> h)
    {
        Span<byte> hash = stackalloc byte[64];
        SodiumBaseline.Hash512(hash, "Mrbr.Cryptography.PedersenVss/v1/H/ristretto255"u8);
        SodiumBaseline.PointFromHash(h, hash);
    }

    internal static void Commit(ReadOnlySpan<byte> value, ReadOnlySpan<byte> blind, ReadOnlySpan<byte> h, Span<byte> result)
    {
        Span<byte> points = stackalloc byte[64];
        try
        {
            MultiplyBase(value, points[..32]);
            MultiplyPublicPoint(blind, h, points[32..]);
            SodiumBaseline.PointAdd(result, points[..32], points[32..]);
        }
        finally { CryptographicOperations.ZeroMemory(points); }
    }

    internal static void MultiplyBase(ReadOnlySpan<byte> scalar, Span<byte> result)
    {
        Span<byte> adjusted = stackalloc byte[32];
        try
        {
            int mask = AdjustZero(scalar, adjusted);
            if (SodiumBaseline.BaseMultiply(result, adjusted) != 0) throw new CryptographicException("RistrettoBaseline base multiplication failed.");
            for (int i = 0; i < 32; i++) result[i] = (byte)(result[i] & mask);
        }
        finally { CryptographicOperations.ZeroMemory(adjusted); }
    }

    internal static void MultiplyPublicPoint(ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> publicPoint, Span<byte> result)
    {
        // The point/identity branch is public. Secret zero scalars instead use
        // a mask: multiply by one, then select the encoded identity, branch-free.
        if (publicPoint.IndexOfAnyExcept((byte)0) < 0) { result.Clear(); return; }
        Span<byte> adjusted = stackalloc byte[32];
        try
        {
            int mask = AdjustZero(scalar, adjusted);
            if (SodiumBaseline.PointMultiply(result, adjusted, publicPoint) != 0) throw new CryptographicException("RistrettoBaseline point multiplication failed.");
            for (int i = 0; i < 32; i++) result[i] = (byte)(result[i] & mask);
        }
        finally { CryptographicOperations.ZeroMemory(adjusted); }
    }

    private static int AdjustZero(ReadOnlySpan<byte> scalar, Span<byte> adjusted)
    {
        scalar.CopyTo(adjusted);
        int aggregate = 0;
        for (int i = 0; i < 32; i++) aggregate |= scalar[i];
        int nonzero = (aggregate | -aggregate) >> 31 & 1;
        adjusted[0] |= (byte)(1 ^ nonzero);
        return -nonzero;
    }

    internal static void Evaluate(ReadOnlySpan<byte> coefficients, byte coordinate, Span<byte> result)
    {
        Span<byte> x = stackalloc byte[32];
        Span<byte> product = stackalloc byte[32];
        x.Clear(); x[0] = coordinate;
        try
        {
            coefficients[^32..].CopyTo(result);
            for (int offset = coefficients.Length - 64; offset >= 0; offset -= 32)
            {
                SodiumBaseline.ScalarMultiply(product, result, x);
                SodiumBaseline.ScalarAdd(result, product, coefficients.Slice(offset, 32));
            }
        }
        finally { CryptographicOperations.ZeroMemory(product); }
    }

    internal static void Weights(ReadOnlySpan<byte> coordinates, Span<byte> weights)
    {
        Span<byte> scratch = stackalloc byte[32 * 6];
        Span<byte> numerator = scratch[..32], denominator = scratch.Slice(32, 32), xi = scratch.Slice(64, 32), xj = scratch.Slice(96, 32), difference = scratch.Slice(128, 32), temp = scratch.Slice(160, 32);
        try
        {
            for (int i = 0; i < coordinates.Length; i++)
            {
                numerator.Clear(); numerator[0] = 1;
                denominator.Clear(); denominator[0] = 1;
                xi.Clear(); xi[0] = coordinates[i];
                for (int j = 0; j < coordinates.Length; j++)
                {
                    if (i == j) continue;
                    xj.Clear(); xj[0] = coordinates[j];
                    SodiumBaseline.ScalarMultiply(temp, numerator, xj); temp.CopyTo(numerator);
                    // In the prime field, subtraction is not XOR:
                    // w_i = product(j!=i) x_j / (x_j - x_i).
                    SodiumBaseline.ScalarSubtract(difference, xj, xi);
                    SodiumBaseline.ScalarMultiply(temp, denominator, difference); temp.CopyTo(denominator);
                }
                SodiumBaseline.ScalarInverse(temp, denominator);
                SodiumBaseline.ScalarMultiply(weights.Slice(i * 32, 32), numerator, temp);
            }
        }
        finally { CryptographicOperations.ZeroMemory(scratch); }
    }
}
