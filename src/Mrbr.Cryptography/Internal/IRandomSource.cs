using System.Security.Cryptography;

namespace Mrbr.Cryptography.Internal;

// An internal test seam. Production always uses the OS cryptographic RNG;
// constrained generic calls on the value-type source do not require boxing.
internal interface IRandomSource
{
    void Fill(Span<byte> destination);
}

internal readonly struct CryptographicRandomSource : IRandomSource
{
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
