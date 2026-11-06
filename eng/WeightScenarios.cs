namespace Mrbr.Cryptography.WeightMeasurements;

internal static class WeightScenarios
{
    internal static byte[] Coordinates(int count, bool sparse)
    {
        byte[] result = new byte[count];
        for (int i = 0; i < count; i++)
        {
            // 73 is coprime to 255: this permutation covers every nonzero byte.
            result[i] = sparse ? (byte)((254 + 73 * i) % 255 + 1) : (byte)(i + 1);
        }
        return result;
    }

    internal static byte[] Shares(int length, int count, bool sparse)
    {
        byte[] coordinates = Coordinates(count, sparse);
        byte[] shares = new byte[(length + 1) * count];
        for (int i = 0; i < count; i++)
        {
            shares[i * (length + 1)] = coordinates[i];
            // Arbitrary payloads also test exact interpolation semantics when
            // shares are corrupted/mixed: no authenticity is assumed.
            for (int j = 0; j < length; j++)
                shares[i * (length + 1) + 1 + j] = (byte)((i * 97 + j * 31 + 19) & 255);
        }
        return shares;
    }
}
