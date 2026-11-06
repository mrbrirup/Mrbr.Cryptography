namespace Mrbr.Cryptography.Vss.Tests;

public sealed class AllocationTests
{
    [Theory]
    [InlineData(0, 2, 3)]
    [InlineData(8, 2, 3)]
    [InlineData(16, 3, 5)]
    [InlineData(24, 3, 5)]
    [InlineData(32, 3, 5)]
    [InlineData(32, 5, 7)]
    [InlineData(32, 9, 9)]
    [InlineData(1024, 3, 5)]
    public void WarmPublicSuccessAndUntrustedFailurePathsAllocateZeroManagedBytes(int length, int threshold, int count)
    {
        byte[] secret = new byte[length];
        var (shares, commitments) = PedersenVssTests.Create(secret, threshold, count);
        byte[] output = new byte[length];
        int shareLength = PedersenVss.GetShareLength(length);
        int selectedLength = shareLength * threshold;
        PedersenVss.VerifyShare(shares.AsSpan(0, shareLength), commitments);
        PedersenVss.Reconstruct(shares.AsSpan(0, selectedLength), commitments, output);
        _ = GC.GetAllocatedBytesForCurrentThread();
        for (int pass = 0; pass < 3; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            int created = PedersenVss.CreateShares(secret, threshold, count, shares, commitments);
            long createBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            bool valid = PedersenVss.VerifyShare(shares.AsSpan(0, shareLength), commitments);
            long verifyBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            bool reconstructed = PedersenVss.TryReconstruct(shares.AsSpan(0, selectedLength), commitments, output, out int written);
            long reconstructBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            int recovered = PedersenVss.Reconstruct(shares.AsSpan(0, selectedLength), commitments, output);
            long wrapperBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            shares[0] ^= 1;
            before = GC.GetAllocatedBytesForCurrentThread();
            bool badVerify = PedersenVss.VerifyShare(shares.AsSpan(0, shareLength), commitments);
            bool badReconstruct = PedersenVss.TryReconstruct(shares.AsSpan(0, selectedLength), commitments, output, out _);
            long failureBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            shares[0] ^= 1;
            Assert.Equal(shares.Length, created); Assert.True(valid); Assert.True(reconstructed);
            Assert.Equal(length, written); Assert.Equal(length, recovered);
            Assert.False(badVerify); Assert.False(badReconstruct);
            Assert.Equal(0L, createBytes); Assert.Equal(0L, verifyBytes);
            Assert.Equal(0L, reconstructBytes); Assert.Equal(0L, wrapperBytes); Assert.Equal(0L, failureBytes);
            Assert.Equal(secret, output);
        }
    }
}
