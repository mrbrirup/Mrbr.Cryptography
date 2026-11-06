using System.Runtime.CompilerServices;

namespace Mrbr.Cryptography.Internal;

/// <summary>
/// Arithmetic in GF(2^8), with bytes representing polynomials over GF(2).
/// </summary>
/// <remarks>
/// The modulus is x^8 + x^4 + x^3 + x + 1 (0x11B).
/// Successful operations allocate no managed memory and perform no I/O.
/// Multiplication has eight iterations and no operand-dependent branches or tables.
/// This source-level structure is not a constant-time guarantee for every JIT/CPU.
/// </remarks>
internal static class Gf256
{
    private const int Modulus = 0x11B;

    /// <summary>Adds two field elements by adding their coefficients modulo two.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Add(byte left, byte right) => (byte)(left ^ right);

    /// <summary>Subtracts two field elements; subtraction equals addition in characteristic two.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Subtract(byte left, byte right) => (byte)(left ^ right);

    /// <summary>Multiplies two field elements, reducing modulo 0x11B.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Multiply(byte left, byte right)
    {
        int multiplicand = left;
        int multiplier = right;
        int product = 0;

        for (int bit = 0; bit < 8; bit++)
        {
            // A bit becomes either mask 0 or mask -1, selecting without a branch.
            product ^= multiplicand & -(multiplier & 1);

            // Multiply by x. If x^8 appears, cancel it using the full modulus.
            // The result stays within a byte; no overflowing byte cast is needed.
            multiplicand = (multiplicand << 1) ^ (Modulus & -(multiplicand >> 7));
            multiplier >>= 1;
        }

        return (byte)product;
    }

    /// <summary>Returns the multiplicative inverse of a nonzero field element.</summary>
    /// <exception cref="DivideByZeroException">The value is zero, which has no inverse.</exception>
    internal static byte Inverse(byte value)
    {
        if (value == 0)
        {
            throw new DivideByZeroException();
        }

        // Nonzero elements form a group of order 255: a^255 = 1, so a^-1 = a^254.
        // This fixed addition chain needs seven squarings and four other products.
        byte a2 = Multiply(value, value);
        byte a3 = Multiply(a2, value);
        byte a6 = Multiply(a3, a3);
        byte a12 = Multiply(a6, a6);
        byte a15 = Multiply(a12, a3);
        byte a30 = Multiply(a15, a15);
        byte a60 = Multiply(a30, a30);
        byte a120 = Multiply(a60, a60);
        byte a240 = Multiply(a120, a120);
        byte a252 = Multiply(a240, a12);
        return Multiply(a252, a2);
    }

    /// <summary>Divides by multiplying the numerator by the denominator's inverse.</summary>
    /// <exception cref="DivideByZeroException">The denominator is zero, including for 0/0.</exception>
    internal static byte Divide(byte numerator, byte denominator) => Multiply(numerator, Inverse(denominator));
}
