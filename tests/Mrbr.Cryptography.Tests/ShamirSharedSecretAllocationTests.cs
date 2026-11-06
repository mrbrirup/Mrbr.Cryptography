namespace Mrbr.Cryptography.Tests;

public sealed class ShamirSharedSecretAllocationTests
{
    [Theory]
    [InlineData(0, 2, 2)]
    [InlineData(32, 3, 5)]
    [InlineData(15, 3, 5)]
    [InlineData(16, 3, 5)]
    [InlineData(31, 3, 5)]
    [InlineData(33, 3, 5)]
    [InlineData(65, 5, 7)]
    [InlineData(65536, 3, 5)]
    [InlineData(4097, 2, 3)]
    [InlineData(17, 255, 255)]
    public void PublicApis_AllocateNoManagedMemory_AfterWarmup(int length, int threshold, int count)
    {
        byte[] secret = new byte[length];
        for (int index = 0; index < length; index++)
        {
            secret[index] = (byte)(index & 0xFF);
        }

        byte[] shares = new byte[ShamirSharedSecret.GetSharesLength(length, count)];
        byte[] recovered = new byte[length];
        int shareLength = ShamirSharedSecret.GetShareLength(length);

        // Warm the public API, including the real platform RNG, before measuring.
        ShamirSharedSecret.CreateShares(secret, threshold, count, shares);
        ShamirSharedSecret.Reconstruct(shares, shareLength, threshold, recovered);
        _ = GC.GetAllocatedBytesForCurrentThread();

        long beforeCreation = GC.GetAllocatedBytesForCurrentThread();
        int created = ShamirSharedSecret.CreateShares(secret, threshold, count, shares);
        long creationAllocations = GC.GetAllocatedBytesForCurrentThread() - beforeCreation;

        long beforeReconstruction = GC.GetAllocatedBytesForCurrentThread();
        int reconstructed = ShamirSharedSecret.Reconstruct(shares, shareLength, threshold, recovered);
        long reconstructionAllocations = GC.GetAllocatedBytesForCurrentThread() - beforeReconstruction;

        Assert.Equal(shares.Length, created);
        Assert.Equal(length, reconstructed);
        Assert.Equal(secret, recovered);
        Assert.Equal(0L, creationAllocations);
        Assert.Equal(0L, reconstructionAllocations);
    }
}
