// Frozen SIMD-baseline reconstruction; extracted before weight optimization.
// Validation, weight loop, payload kernels and dispatch are unchanged.
using Mrbr.Cryptography.Internal;

namespace Mrbr.Cryptography.WeightBenchmarks.Baselines;

internal static class SimdBaseline
{
    private const int MaximumShareCount = 255;

    public static int Reconstruct(ReadOnlySpan<byte> shares, int shareLength, int threshold, Span<byte> destination)
        => Reconstruct(shares, shareLength, threshold, destination, Gf256Implementation.Auto);

    internal static int Reconstruct(ReadOnlySpan<byte> shares, int shareLength, int threshold, Span<byte> destination,
        Gf256Implementation implementation)
    {
        int vectorWidth = Gf256Simd.GetVectorWidth(implementation);
        ValidateThreshold(threshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(shareLength, 1);

        if (shares.Length % shareLength != 0)
        {
            throw new ArgumentException("The buffer must contain complete, equally sized shares.", nameof(shares));
        }

        int shareCount = shares.Length / shareLength;
        if (shareCount < threshold || shareCount > MaximumShareCount)
        {
            throw new ArgumentException("The share count must be at least the threshold and at most 255.", nameof(shares));
        }

        int secretLength = shareLength - 1;
        if (destination.Length < secretLength)
        {
            throw new ArgumentException("The destination is too small for the secret.", nameof(destination));
        }

        if (shares.Overlaps(destination))
        {
            throw new ArgumentException("The shares and destination must not overlap.", nameof(destination));
        }

        // All stack allocations are bounded by the validated 255-share maximum.
        Span<byte> coordinates = stackalloc byte[shareCount];
        Span<byte> seen = stackalloc byte[256];
        seen.Clear();

        for (int share = 0; share < shareCount; share++)
        {
            byte coordinate = shares[share * shareLength];
            if (coordinate == 0 || seen[coordinate] != 0)
            {
                throw new ArgumentException("Share coordinates must be distinct and nonzero.", nameof(shares));
            }

            seen[coordinate] = 1;
            coordinates[share] = coordinate;
        }

        if (secretLength == 0)
        {
            return 0;
        }

        // At zero, L_i(0) = product(j != i) x_j / (x_i + x_j).
        // Minus equals plus in characteristic two. Compute products first so
        // each weight needs one inversion, and reuse weights for every byte.
        Span<byte> weights = stackalloc byte[shareCount];
        for (int share = 0; share < shareCount; share++)
        {
            byte numerator = 1;
            byte denominator = 1;
            for (int other = 0; other < shareCount; other++)
            {
                if (other != share)
                {
                    numerator = Gf256.Multiply(numerator, coordinates[other]);
                    denominator = Gf256.Multiply(denominator, Gf256.Add(coordinates[share], coordinates[other]));
                }
            }

            weights[share] = Gf256.Divide(numerator, denominator);
        }

        Span<byte> output = destination[..secretLength];
        output.Clear();
        for (int share = 0; share < shareCount; share++)
        {
            ReadOnlySpan<byte> payload = shares.Slice((share * shareLength) + 1, secretLength);
            Gf256Simd.MultiplyAdd(payload, weights[share], output, vectorWidth);
        }

        return secretLength;
    }

    private static void ValidateThreshold(int threshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, MaximumShareCount);
    }
}
