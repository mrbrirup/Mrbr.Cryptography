namespace Mrbr.Cryptography.Tests;

public sealed class ShamirSharedSecretValidationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void SizeHelpers_RejectNegativeSecretLength(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShamirSharedSecret.GetShareLength(length));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShamirSharedSecret.GetSharesLength(length, 2));
    }

    [Fact]
    public void SizeHelpers_RejectOverflowInsteadOfWrapping()
    {
        Assert.Throws<OverflowException>(() => ShamirSharedSecret.GetShareLength(int.MaxValue));
        Assert.Throws<OverflowException>(() => ShamirSharedSecret.GetSharesLength(int.MaxValue, 2));
        Assert.Throws<OverflowException>(() => ShamirSharedSecret.GetSharesLength(int.MaxValue - 1, 2));
    }

    [Theory]
    [InlineData(2, -1, "shareCount")]
    [InlineData(2, 0, "shareCount")]
    [InlineData(2, 1, "shareCount")]
    [InlineData(2, 256, "shareCount")]
    [InlineData(2, int.MaxValue, "shareCount")]
    [InlineData(-1, 5, "threshold")]
    [InlineData(0, 5, "threshold")]
    [InlineData(1, 5, "threshold")]
    [InlineData(256, 255, "threshold")]
    [InlineData(3, 2, "threshold")]
    public void CreateShares_InvalidParameters_DoNotWriteOrRequestEntropy(int threshold, int count, string parameter)
    {
        byte[] secret = [0x42];
        byte[] destination = new byte[1024];
        destination.AsSpan().Fill(0xCC);
        var source = new ShamirSharedSecretTests.CountingRandomSource();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ShamirSharedSecret.CreateShares(secret, threshold, count, destination, source));

        Assert.Equal(parameter, exception.ParamName);
        Assert.All(destination, value => Assert.Equal((byte)0xCC, value));
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void CreateShares_ShortDestination_DoesNotWriteOrRequestEntropy()
    {
        byte[] secret = [1, 2, 3];
        byte[] destination = new byte[19]; // Five shares need 20 bytes.
        destination.AsSpan().Fill(0xCC);
        var source = new ShamirSharedSecretTests.CountingRandomSource();

        var exception = Assert.Throws<ArgumentException>(() => ShamirSharedSecret.CreateShares(secret, 3, 5, destination, source));

        Assert.Equal("destination", exception.ParamName);
        Assert.All(destination, value => Assert.Equal((byte)0xCC, value));
        Assert.Equal(0, source.CallCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    public void CreateShares_Overlap_IsRejectedBeforeWriting(int secretOffset, int destinationOffset)
    {
        byte[] buffer = new byte[20];
        buffer.AsSpan().Fill(0xCC);
        byte[] original = (byte[])buffer.Clone();

        Assert.Throws<ArgumentException>(() => ShamirSharedSecret.CreateShares(
            buffer.AsSpan(secretOffset, 3), 2, 3, buffer.AsSpan(destinationOffset, 12)));

        Assert.Equal(original, buffer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    public void Reconstruct_InvalidThreshold_LeavesDestinationUnchanged(int threshold)
    {
        byte[] destination = [0xCC];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ShamirSharedSecret.Reconstruct(new byte[] { 1, 0x55, 2, 0xAA }, 2, threshold, destination));

        Assert.Equal("threshold", exception.ParamName);
        Assert.Equal((byte)0xCC, destination[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Reconstruct_InvalidShareLength_LeavesDestinationUnchanged(int length)
    {
        byte[] destination = [0xCC];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ShamirSharedSecret.Reconstruct(new byte[] { 1, 0x55, 2, 0xAA }, length, 2, destination));

        Assert.Equal("shareLength", exception.ParamName);
        Assert.Equal((byte)0xCC, destination[0]);
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 3)]
    [InlineData(256, 1, 2)]
    [InlineData(5, 2, 2)]
    public void Reconstruct_InvalidFramingOrCount_LeavesDestinationUnchanged(int bufferLength, int shareLength, int threshold)
    {
        byte[] shares = new byte[bufferLength];
        byte[] destination = [0xCC, 0xCC, 0xCC];

        var exception = Assert.Throws<ArgumentException>(
            () => ShamirSharedSecret.Reconstruct(shares, shareLength, threshold, destination));

        Assert.Equal("shares", exception.ParamName);
        Assert.All(destination, value => Assert.Equal((byte)0xCC, value));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public void Reconstruct_InvalidCoordinates_LeavesDestinationUnchanged(byte first, byte second)
    {
        byte[] shares = [first, 0x55, second, 0xAA];
        byte[] destination = [0xCC];

        var exception = Assert.Throws<ArgumentException>(() => ShamirSharedSecret.Reconstruct(shares, 2, 2, destination));

        Assert.Equal("shares", exception.ParamName);
        Assert.Equal((byte)0xCC, destination[0]);
    }

    [Fact]
    public void Reconstruct_ValidatesCoordinatesOfExtraShares()
    {
        byte[] shares = [1, 0x55, 2, 0xAA, 1, 0xFF];
        byte[] destination = [0xCC];

        Assert.Throws<ArgumentException>(() => ShamirSharedSecret.Reconstruct(shares, 2, 2, destination));

        Assert.Equal((byte)0xCC, destination[0]);
    }

    [Fact]
    public void Reconstruct_EmptyPayload_StillValidatesCoordinates()
    {
        Assert.Throws<ArgumentException>(() => ShamirSharedSecret.Reconstruct(new byte[] { 1, 1 }, 1, 2, []));
        Assert.Throws<ArgumentException>(() => ShamirSharedSecret.Reconstruct(new byte[] { 0, 2 }, 1, 2, []));
    }

    [Fact]
    public void Reconstruct_ShortDestination_IsRejectedBeforeWriting()
    {
        byte[] destination = [0xCC];

        Assert.Throws<ArgumentException>(
            () => ShamirSharedSecret.Reconstruct(new byte[] { 1, 0x55, 0x55, 2, 0xAA, 0xAA }, 3, 2, destination));

        Assert.Equal((byte)0xCC, destination[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void Reconstruct_Overlap_IsRejectedBeforeWriting(int destinationOffset)
    {
        byte[] buffer = [1, 0x55, 0x55, 2, 0xAA, 0xAA, 0xCC, 0xCC];
        byte[] original = (byte[])buffer.Clone();

        Assert.Throws<ArgumentException>(
            () => ShamirSharedSecret.Reconstruct(buffer.AsSpan(0, 6), 3, 2, buffer.AsSpan(destinationOffset, 2)));

        Assert.Equal(original, buffer);
    }
}
