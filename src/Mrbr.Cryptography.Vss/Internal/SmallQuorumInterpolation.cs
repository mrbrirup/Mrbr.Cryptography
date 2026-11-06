using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Mrbr.Cryptography.Vss.Internal;

/// <summary>Full scalar-polynomial interpolation for 2..9 validated public coordinates.</summary>
internal static class SmallQuorumInterpolation
{
    internal const int MaximumCount = 9;

    // Matrix row i contains the ascending coefficients of L_i(X). Caller
    // validates distinct coordinates in 1..255 and provides count^2*32 bytes.
    internal static void BuildBasis(ReadOnlySpan<byte> coordinates, Span<byte> matrix)
    {
        int count = coordinates.Length;
        Span<Int128> polynomial = stackalloc Int128[MaximumCount];
        Span<byte> denominators = stackalloc byte[count * 32];
        Span<byte> prefixes = stackalloc byte[count * 32];
        Span<byte> inverses = stackalloc byte[count * 32];
        Span<byte> scratch = stackalloc byte[96];
        Span<byte> inverseProduct = scratch[..32], temp = scratch.Slice(32, 32), scaled = scratch.Slice(64, 32);

        for (int i = 0; i < count; i++)
        {
            polynomial.Clear(); polynomial[0] = 1;
            Int128 denominator = 1;
            int degree = 0;
            for (int other = 0; other < count; other++)
            {
                if (other == i) continue;
                int coordinate = coordinates[other];
                for (int coefficient = degree + 1; coefficient > 0; coefficient--)
                    polynomial[coefficient] = polynomial[coefficient - 1] - coordinate * polynomial[coefficient];
                polynomial[0] *= -coordinate;
                denominator *= coordinates[i] - coordinate;
                degree++;
            }
            // With <=8 factors and coordinates <=255, sum(abs(coefficients))
            // <=256^8, strictly smaller for distinct inputs. All magnitudes fit
            // UInt64; signed Int128 makes every intermediate safely exact.
            for (int coefficient = 0; coefficient < count; coefficient++)
                EncodeSigned(polynomial[coefficient], matrix.Slice((i * count + coefficient) * 32, 32));
            EncodeSigned(denominator, denominators.Slice(i * 32, 32));
        }

        // Montgomery batch inversion: one inverse and 3*(count-1) products.
        // Nonzero distinct coordinates guarantee every denominator is nonzero.
        denominators[..32].CopyTo(prefixes);
        for (int i = 1; i < count; i++)
            Sodium.ScalarMultiply(prefixes.Slice(i * 32, 32), prefixes.Slice((i - 1) * 32, 32), denominators.Slice(i * 32, 32));
        Sodium.ScalarInverse(inverseProduct, prefixes.Slice((count - 1) * 32, 32));
        for (int i = count - 1; i > 0; i--)
        {
            Sodium.ScalarMultiply(inverses.Slice(i * 32, 32), inverseProduct, prefixes.Slice((i - 1) * 32, 32));
            Sodium.ScalarMultiply(temp, inverseProduct, denominators.Slice(i * 32, 32)); temp.CopyTo(inverseProduct);
        }
        inverseProduct.CopyTo(inverses);
        for (int i = 0; i < count; i++)
        {
            for (int coefficient = 0; coefficient < count; coefficient++)
            {
                Span<byte> cell = matrix.Slice((i * count + coefficient) * 32, 32);
                Sodium.ScalarMultiply(scaled, cell, inverses.Slice(i * 32, 32)); scaled.CopyTo(cell);
            }
        }
    }

    private static void EncodeSigned(Int128 value, Span<byte> result)
    {
        result.Clear();
        if (value >= 0)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(result, (ulong)value);
            return;
        }
        Span<byte> scratch = stackalloc byte[64];
        scratch.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(scratch[32..], (ulong)(-value));
        Sodium.ScalarSubtract(result, scratch[..32], scratch[32..]);
    }

    internal static void InterpolatePair(ReadOnlySpan<byte> shares, int shareLength, int block,
        ReadOnlySpan<byte> matrix, int count, Span<byte> a, Span<byte> b)
    {
        a.Clear(); b.Clear();
        Span<byte> scratch = stackalloc byte[64];
        try
        {
            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<byte> y = shares.Slice(i * shareLength + PedersenVss.ShareHeaderLength + block * 64, 32);
                ReadOnlySpan<byte> z = shares.Slice(i * shareLength + PedersenVss.ShareHeaderLength + block * 64 + 32, 32);
                for (int coefficient = 0; coefficient < count; coefficient++)
                {
                    ReadOnlySpan<byte> factor = matrix.Slice((i * count + coefficient) * 32, 32);
                    Span<byte> targetA = a.Slice(coefficient * 32, 32), targetB = b.Slice(coefficient * 32, 32);
                    Sodium.ScalarMultiply(scratch[..32], factor, y);
                    Sodium.ScalarAdd(scratch[32..], targetA, scratch[..32]); scratch[32..].CopyTo(targetA);
                    Sodium.ScalarMultiply(scratch[..32], factor, z);
                    Sodium.ScalarAdd(scratch[32..], targetB, scratch[..32]); scratch[32..].CopyTo(targetB);
                }
            }
        }
        finally { CryptographicOperations.ZeroMemory(scratch); }
    }

    internal static bool VerifyCoefficients(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b,
        ReadOnlySpan<byte> commitments, ReadOnlySpan<byte> h)
    {
        Span<byte> point = stackalloc byte[32], identity = stackalloc byte[32];
        identity.Clear();
        bool valid = true;
        try
        {
            for (int coefficient = 0; coefficient < a.Length / 32; coefficient++)
            {
                Ristretto.Commit(a.Slice(coefficient * 32, 32), b.Slice(coefficient * 32, 32), h, point);
                ReadOnlySpan<byte> expected = coefficient < commitments.Length / 32
                    ? commitments.Slice(coefficient * 32, 32) : identity;
                // Surplus coefficients must OPEN the identity, not necessarily
                // have two zero scalars. Evaluate every check without exposing
                // a first-matching-coefficient index through early rejection.
                valid &= CryptographicOperations.FixedTimeEquals(point, expected);
            }
            return valid;
        }
        finally { CryptographicOperations.ZeroMemory(point); }
    }
}
