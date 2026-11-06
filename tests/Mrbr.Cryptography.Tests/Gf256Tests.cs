using Mrbr.Cryptography.Internal;

namespace Mrbr.Cryptography.Tests;

public sealed class Gf256Tests
{
    [Theory]
    [InlineData(0x57, 0x83, 0xD4)] // NIST FIPS 197, section 4.1.
    [InlineData(0x00, 0xA5, 0xA5)]
    [InlineData(0xFF, 0xFF, 0x00)]
    public void AddAndSubtract_MatchKnownExamples(byte left, byte right, byte expected)
    {
        Assert.Equal(expected, Gf256.Add(left, right));
        Assert.Equal(expected, Gf256.Subtract(left, right));
    }

    [Fact]
    public void Addition_HasIdentityAndEveryElementIsItsOwnAdditiveInverse()
    {
        for (int value = 0; value < 256; value++)
        {
            byte element = (byte)value;

            Assert.Equal(element, Gf256.Add(element, 0));
            Assert.Equal(element, Gf256.Add(0, element));
            Assert.Equal((byte)0, Gf256.Add(element, element));
            Assert.Equal((byte)0, Gf256.Subtract(element, element));
        }
    }

    [Fact]
    public void Subtraction_UndoesAddition_ForEveryBytePair()
    {
        for (int left = 0; left < 256; left++)
        {
            for (int right = 0; right < 256; right++)
            {
                byte sum = Gf256.Add((byte)left, (byte)right);

                Assert.Equal(sum, Gf256.Add((byte)right, (byte)left));
                Assert.Equal((byte)left, Gf256.Subtract(sum, (byte)right));
            }
        }
    }

    // Published examples from NIST FIPS 197, section 4.2, plus the x^8 reduction.
    [Theory]
    [InlineData(0x57, 0x01, 0x57)]
    [InlineData(0x57, 0x02, 0xAE)]
    [InlineData(0x57, 0x04, 0x47)]
    [InlineData(0x57, 0x08, 0x8E)]
    [InlineData(0x57, 0x10, 0x07)]
    [InlineData(0x57, 0x20, 0x0E)]
    [InlineData(0x57, 0x40, 0x1C)]
    [InlineData(0x57, 0x80, 0x38)]
    [InlineData(0x57, 0x13, 0xFE)]
    [InlineData(0x80, 0x02, 0x1B)]
    public void Multiply_MatchesKnownExamples(byte left, byte right, byte expected)
    {
        Assert.Equal(expected, Gf256.Multiply(left, right));
    }

    [Fact]
    public void Multiply_MatchesIndependentPolynomialReference_ForEveryBytePair()
    {
        for (int left = 0; left < 256; left++)
        {
            for (int right = 0; right < 256; right++)
            {
                byte expected = ReferenceMultiply((byte)left, (byte)right);

                Assert.Equal(expected, Gf256.Multiply((byte)left, (byte)right));
            }
        }
    }

    [Fact]
    public void Multiplication_HasZeroAndOneIdentities()
    {
        for (int value = 0; value < 256; value++)
        {
            byte element = (byte)value;

            Assert.Equal((byte)0, Gf256.Multiply(element, 0));
            Assert.Equal((byte)0, Gf256.Multiply(0, element));
            Assert.Equal(element, Gf256.Multiply(element, 1));
            Assert.Equal(element, Gf256.Multiply(1, element));
        }
    }

    [Fact]
    public void Multiplication_DistributesOverAddition_ForEveryBytePairAndBasisElement()
    {
        // Testing every byte plus each basis element (1, x, ..., x^7) covers
        // the transitions needed to build any second addend from its bits.
        for (int left = 0; left < 256; left++)
        {
            for (int right = 0; right < 256; right++)
            {
                byte product = Gf256.Multiply((byte)left, (byte)right);

                for (int bit = 0; bit < 8; bit++)
                {
                    byte basis = (byte)(1 << bit);
                    byte expanded = Gf256.Add(product, Gf256.Multiply((byte)left, basis));
                    byte factored = Gf256.Multiply((byte)left, Gf256.Add((byte)right, basis));

                    Assert.Equal(expanded, factored);
                }
            }
        }
    }

    [Fact]
    public void Inverse_MatchesIndependentSearch_ForEveryNonzeroByte()
    {
        for (int value = 1; value < 256; value++)
        {
            byte inverse = Gf256.Inverse((byte)value);

            Assert.Equal(ReferenceInverse((byte)value), inverse);
            Assert.Equal((byte)1, ReferenceMultiply((byte)value, inverse));
        }
    }

    [Fact]
    public void Inverse_Zero_ThrowsDivideByZeroException()
    {
        Assert.Throws<DivideByZeroException>(() => Gf256.Inverse(0));
    }

    [Fact]
    public void Divide_MatchesIndependentReference_ForEveryValidBytePair()
    {
        Span<byte> inverses = stackalloc byte[256];
        for (int denominator = 1; denominator < 256; denominator++)
        {
            inverses[denominator] = ReferenceInverse((byte)denominator);
        }

        for (int numerator = 0; numerator < 256; numerator++)
        {
            for (int denominator = 1; denominator < 256; denominator++)
            {
                byte expected = ReferenceMultiply((byte)numerator, inverses[denominator]);

                Assert.Equal(expected, Gf256.Divide((byte)numerator, (byte)denominator));
            }
        }
    }

    [Fact]
    public void Divide_ByZero_ThrowsForEveryNumerator()
    {
        for (int numerator = 0; numerator < 256; numerator++)
        {
            byte element = (byte)numerator;

            Assert.Throws<DivideByZeroException>(() => Gf256.Divide(element, 0));
        }
    }

    // Independent oracle: build the entire carry-less polynomial product first,
    // then perform polynomial long division. Production reduces each shift instead.
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

    // No use of production inversion or exponentiation: search all candidates.
    private static byte ReferenceInverse(byte value)
    {
        for (int candidate = 1; candidate < 256; candidate++)
        {
            if (ReferenceMultiply(value, (byte)candidate) == 1)
            {
                return (byte)candidate;
            }
        }

        throw new InvalidOperationException($"No inverse exists for {value:X2}.");
    }
}
