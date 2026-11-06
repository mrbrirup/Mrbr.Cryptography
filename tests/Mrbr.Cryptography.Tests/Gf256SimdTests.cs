using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Mrbr.Cryptography.Internal;

namespace Mrbr.Cryptography.Tests;

public sealed class Gf256SimdTests
{
    [Fact]
    public void Multiply128_MatchesIndependentReference_ForEveryBytePair()
    {
        Assert.SkipUnless(Gf256Simd.Is128Supported, "Requires SSSE3 or ARM64 AdvSimd.");
        Span<byte> input = stackalloc byte[16];
        Span<byte> output = stackalloc byte[16];

        for (int factor = 0; factor < 256; factor++)
        {
            var lookup = new Gf256Lookup((byte)factor);
            for (int start = 0; start < 256; start += 16)
            {
                for (int lane = 0; lane < 16; lane++)
                {
                    input[lane] = (byte)(start + lane);
                }

                var value = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(input));
                Gf256Simd.Multiply128(value, lookup.Low, lookup.High).StoreUnsafe(ref MemoryMarshal.GetReference(output));

                for (int lane = 0; lane < 16; lane++)
                {
                    Assert.Equal(ReferenceMultiply(input[lane], (byte)factor), output[lane]);
                }
            }
        }
    }

    [Fact]
    public void Multiply256_MatchesIndependentReference_ForEveryBytePairAndBothShuffleLanes()
    {
        Assert.SkipUnless(Gf256Simd.Is256Supported, "Requires AVX2.");
        Span<byte> input = stackalloc byte[32];
        Span<byte> output = stackalloc byte[32];

        for (int factor = 0; factor < 256; factor++)
        {
            var lookup = new Gf256Lookup((byte)factor);
            var low = Vector256.Create(lookup.Low, lookup.Low);
            var high = Vector256.Create(lookup.High, lookup.High);
            // Slide by half a vector so each byte value is tested in both halves.
            for (int start = 0; start < 256; start += 16)
            {
                for (int lane = 0; lane < 32; lane++)
                {
                    input[lane] = (byte)((start + lane) & 0xFF);
                }

                var value = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(input));
                Gf256Simd.Multiply256(value, low, high).StoreUnsafe(ref MemoryMarshal.GetReference(output));

                for (int lane = 0; lane < 32; lane++)
                {
                    Assert.Equal(ReferenceMultiply(input[lane], (byte)factor), output[lane]);
                }
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(47)]
    [InlineData(48)]
    [InlineData(49)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(97)]
    public void MultiplyAdd_MatchesReference_AtUnalignedOffsetsAndVectorTails(int length)
    {
        byte[] input = new byte[length + 7];
        for (int position = 0; position < input.Length; position++)
        {
            input[position] = (byte)(((position * 173) + 0x81) & 0xFF);
        }

        byte[] original = (byte[])input.Clone();
        foreach (Gf256Implementation implementation in SupportedImplementations())
        {
            foreach (byte factor in new byte[] { 0, 1, 2, 0x13, 0x80, 0xFF })
            {
                byte[] destination = new byte[length + 9];
                destination.AsSpan().Fill(0xCC);
                destination.AsSpan(3, length).Fill(0xA5);

                Gf256Simd.MultiplyAdd(input.AsSpan(1, length), factor, destination.AsSpan(3, length),
                    Gf256Simd.GetVectorWidth(implementation));

                for (int position = 0; position < length; position++)
                {
                    Assert.Equal((byte)(0xA5 ^ ReferenceMultiply(input[position + 1], factor)), destination[position + 3]);
                }

                Assert.All(destination[..3], value => Assert.Equal((byte)0xCC, value));
                Assert.All(destination[(length + 3)..], value => Assert.Equal((byte)0xCC, value));
            }
        }

        Assert.Equal(original, input);
    }

    internal static IEnumerable<Gf256Implementation> SupportedImplementations()
    {
        yield return Gf256Implementation.Scalar;
        yield return Gf256Implementation.Auto;
        if (Gf256Simd.Is128Supported)
        {
            yield return Gf256Implementation.Vector128;
        }

        if (Gf256Simd.Is256Supported)
        {
            yield return Gf256Implementation.Vector256;
        }
    }

    // Independent oracle: form the carry-less product before polynomial long division.
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
}
