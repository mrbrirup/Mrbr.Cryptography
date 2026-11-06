namespace Mrbr.Cryptography.Internal;

/// <summary>Calculates interpolation-at-zero weights from validated public coordinates.</summary>
/// <remarks>
/// Coordinates must be distinct, nonzero and number 2..255; weights must have
/// sufficient disjoint space. Reconstruct validates these conditions first.
/// All memory-table indices below depend only on public coordinates, never on
/// secret bytes or share payloads. Secret arithmetic stays in Gf256/Gf256Simd.
/// Immutable tables occupy 766 bytes of assembly data; scratch is bounded stack
/// memory. There is no managed table construction, cache, or I/O.
/// </remarks>
internal static partial class LagrangeWeights
{
    internal static void Calculate(ReadOnlySpan<byte> coordinates, Span<byte> weights)
    {
        ReadOnlySpan<byte> log = Logarithms;
        ReadOnlySpan<byte> exp = Exponents;
        int count = coordinates.Length;

        if (count == 2)
        {
            // w0 = x1/(x0+x1), w1 = 1+w0. The doubled exponent period
            // covers indices 1..509, avoiding any remainder operation here.
            byte first = exp[log[coordinates[1]] + 255 - log[coordinates[0] ^ coordinates[1]]];
            weights[0] = first;
            weights[1] = (byte)(first ^ 1);
            return;
        }

        if (count == 3)
        {
            // Unroll the common three-share case, reusing the shared difference.
            int x0 = coordinates[0], x1 = coordinates[1], x2 = coordinates[2];
            int sharedDifference = log[x0 ^ x1];
            byte first = exp[(int)((uint)(log[x1] + log[x2] - sharedDifference - log[x0 ^ x2] + 510) % 255)];
            byte second = exp[(int)((uint)(log[x0] + log[x2] - sharedDifference - log[x1 ^ x2] + 510) % 255)];
            weights[0] = first;
            weights[1] = second;
            weights[2] = (byte)(1 ^ first ^ second);
            return;
        }

        // w_i = P / (x_i * product(j != i)(x_i+x_j)), P = product(j) x_j.
        // In log space, products become sums and division becomes subtraction
        // modulo 255. Each unordered difference contributes to two denominators.
        // Sum_i w_i = 1, so recover the final weight by XORing the others with 1.
        int last = count - 1;
        Span<int> denominatorLogs = stackalloc int[last]; // At most 254 ints.
        int totalLog = log[coordinates[last]];
        for (int i = 0; i < last; i++)
        {
            int coordinateLog = log[coordinates[i]];
            denominatorLogs[i] = coordinateLog;
            totalLog += coordinateLog;
        }

        for (int i = 0; i < last; i++)
        {
            int coordinate = coordinates[i];
            for (int j = i + 1; j < last; j++)
            {
                int differenceLog = log[coordinate ^ coordinates[j]];
                denominatorLogs[i] += differenceLog;
                denominatorLogs[j] += differenceLog;
            }
            denominatorLogs[i] += log[coordinate ^ coordinates[last]];
        }

        byte final = 1;
        for (int i = 0; i < last; i++)
        {
            // Each sum is at most 255*254 = 64,770. Adding 255*255 keeps
            // the unsigned remainder nonnegative without changing the exponent.
            byte weight = exp[(int)((uint)(totalLog - denominatorLogs[i] + 65025) % 255)];
            weights[i] = weight;
            final ^= weight;
        }
        weights[last] = final;
    }
}
