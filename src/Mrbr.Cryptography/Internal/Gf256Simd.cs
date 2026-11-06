using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Mrbr.Cryptography.Internal;

// Internal selection is passed per call for tests; no mutable global dispatch state.
internal enum Gf256Implementation
{
    Auto,
    Scalar,
    Vector128,
    Vector256,
}

/// <summary>Register-resident nibble tables for multiplication by a public field element.</summary>
internal readonly struct Gf256Lookup
{
    internal readonly Vector128<byte> Low;
    internal readonly Vector128<byte> High;

    internal Gf256Lookup(byte factor)
    {
        byte p1 = TimesX(factor);
        byte p2 = TimesX(p1);
        byte p3 = TimesX(p2);
        byte p4 = TimesX(p3);
        byte p5 = TimesX(p4);
        byte p6 = TimesX(p5);
        byte p7 = TimesX(p6);
        Low = CreateTable(factor, p1, p2, p3);
        High = CreateTable(p4, p5, p6, p7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TimesX(byte value) => (byte)((value << 1) ^ (0x11B & -(value >> 7)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateTable(byte p0, byte p1, byte p2, byte p3)
    {
        // Lane i selects the basis products whose bits occur in nibble i.
        // The masks are constants, and the products depend only on the public factor.
        var bit0 = Vector128.Create(0xFF00FF00FF00FF00UL).AsByte();
        var bit1 = Vector128.Create(0xFFFF0000FFFF0000UL).AsByte();
        var bit2 = Vector128.Create(0xFFFFFFFF00000000UL).AsByte();
        var bit3 = Vector128.Create(0UL, ulong.MaxValue).AsByte();
        return (Vector128.Create(p0) & bit0) ^ (Vector128.Create(p1) & bit1)
            ^ (Vector128.Create(p2) & bit2) ^ (Vector128.Create(p3) & bit3);
    }
}

/// <summary>Parallel multiplication by public coordinates or interpolation weights.</summary>
/// <remarks>
/// Secret nibbles index register shuffles, never memory lookup tables. All vector
/// loads/stores below are guarded by complete-vector bounds; tails stay scalar.
/// Feature checks keep unsupported instructions out of the automatic fallback.
/// </remarks>
internal static class Gf256Simd
{
    internal static bool Is128Supported => Ssse3.IsSupported || AdvSimd.Arm64.IsSupported;
    internal static bool Is256Supported => Avx2.IsSupported;

    internal static int GetVectorWidth(Gf256Implementation implementation) => implementation switch
    {
        Gf256Implementation.Auto => Is256Supported ? 32 : Is128Supported ? 16 : 0,
        Gf256Implementation.Scalar => 0,
        Gf256Implementation.Vector128 when Is128Supported => 16,
        Gf256Implementation.Vector256 when Is256Supported => 32,
        Gf256Implementation.Vector128 or Gf256Implementation.Vector256 => throw new PlatformNotSupportedException(),
        _ => throw new ArgumentOutOfRangeException(nameof(implementation)),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> Multiply128(Vector128<byte> value, Vector128<byte> low, Vector128<byte> high)
    {
        var mask = Vector128.Create((byte)0x0F);
        var lo = value & mask;
        // Word shifts can mix adjacent bytes; masking discards those extra bits.
        var hi = Vector128.ShiftRightLogical(value.AsUInt16(), 4).AsByte() & mask;
        return Vector128.Shuffle(low, lo) ^ Vector128.Shuffle(high, hi);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<byte> Multiply256(Vector256<byte> value, Vector256<byte> low, Vector256<byte> high)
    {
        var mask = Vector256.Create((byte)0x0F);
        var lo = value & mask;
        var hi = Avx2.ShiftRightLogical(value.AsUInt16(), 4).AsByte() & mask;
        // AVX2 shuffles address each 128-bit lane independently. Callers duplicate
        // each 16-entry table into both lanes, preserving the same mapping.
        return Avx2.Shuffle(low, lo) ^ Avx2.Shuffle(high, hi);
    }

    // All spans have the same payload length, and destination is disjoint from source.
    internal static void MultiplyAdd(ReadOnlySpan<byte> source, byte factor, Span<byte> destination, int vectorWidth)
    {
        int position = 0;
        if (vectorWidth != 0 && source.Length >= 16)
        {
            var lookup = new Gf256Lookup(factor);
            ref byte input = ref MemoryMarshal.GetReference(source);
            ref byte output = ref MemoryMarshal.GetReference(destination);

            if (vectorWidth == 32)
            {
                var low = Vector256.Create(lookup.Low, lookup.Low);
                var high = Vector256.Create(lookup.High, lookup.High);
                for (; position <= source.Length - 32; position += 32)
                {
                    var product = Multiply256(Vector256.LoadUnsafe(ref input, (nuint)position), low, high);
                    (Vector256.LoadUnsafe(ref output, (nuint)position) ^ product).StoreUnsafe(ref output, (nuint)position);
                }
            }

            if (Is128Supported)
            {
                for (; position <= source.Length - 16; position += 16)
                {
                    var product = Multiply128(Vector128.LoadUnsafe(ref input, (nuint)position), lookup.Low, lookup.High);
                    (Vector128.LoadUnsafe(ref output, (nuint)position) ^ product).StoreUnsafe(ref output, (nuint)position);
                }
            }
        }

        for (; position < source.Length; position++)
        {
            destination[position] ^= Gf256.Multiply(source[position], factor);
        }
    }

    // Coefficients are stored in planes: a1 for every byte, then a2, etc.
    // The caller validates degree >= 1, plane storage, and disjoint spans.
    internal static void EvaluatePolynomial(ReadOnlySpan<byte> secret, ReadOnlySpan<byte> planes,
        int degree, byte coordinate, Span<byte> destination, int vectorWidth)
    {
        var lookup = new Gf256Lookup(coordinate);
        int length = secret.Length;
        int lastPlane = (degree - 1) * length;
        int position = 0;
        ref byte coefficients = ref MemoryMarshal.GetReference(planes);
        ref byte constants = ref MemoryMarshal.GetReference(secret);
        ref byte output = ref MemoryMarshal.GetReference(destination);

        if (vectorWidth == 32)
        {
            var low = Vector256.Create(lookup.Low, lookup.Low);
            var high = Vector256.Create(lookup.High, lookup.High);
            for (; position <= length - 32; position += 32)
            {
                var value = Vector256.LoadUnsafe(ref coefficients, (nuint)(lastPlane + position));
                for (int plane = degree - 2; plane >= 0; plane--)
                {
                    value = Multiply256(value, low, high)
                        ^ Vector256.LoadUnsafe(ref coefficients, (nuint)((plane * length) + position));
                }

                value = Multiply256(value, low, high) ^ Vector256.LoadUnsafe(ref constants, (nuint)position);
                value.StoreUnsafe(ref output, (nuint)position);
            }
        }

        if (Is128Supported)
        {
            for (; position <= length - 16; position += 16)
            {
                var value = Vector128.LoadUnsafe(ref coefficients, (nuint)(lastPlane + position));
                for (int plane = degree - 2; plane >= 0; plane--)
                {
                    value = Multiply128(value, lookup.Low, lookup.High)
                        ^ Vector128.LoadUnsafe(ref coefficients, (nuint)((plane * length) + position));
                }

                value = Multiply128(value, lookup.Low, lookup.High) ^ Vector128.LoadUnsafe(ref constants, (nuint)position);
                value.StoreUnsafe(ref output, (nuint)position);
            }
        }

        for (; position < length; position++)
        {
            byte value = planes[lastPlane + position];
            for (int plane = degree - 2; plane >= 0; plane--)
            {
                value = Gf256.Add(Gf256.Multiply(value, coordinate), planes[(plane * length) + position]);
            }

            destination[position] = Gf256.Add(Gf256.Multiply(value, coordinate), secret[position]);
        }
    }
}
