using System.Runtime.CompilerServices;
using Mrbr.Cryptography.Internal;

namespace Mrbr.Cryptography.Tests;

public sealed class Gf256AllocationTests
{
    [Fact]
    public void ValidOperations_AllocateNoManagedMemory_AfterWarmup()
    {
        // Warm the same code paths before measuring; keep runner/assertion work
        // outside the interval. The synchronous loop stays on this thread.
        _ = GC.GetAllocatedBytesForCurrentThread();
        int expected = ExerciseArithmetic();

        long before = GC.GetAllocatedBytesForCurrentThread();
        int actual = ExerciseArithmetic();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(expected, actual);
        Assert.Equal(0L, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ExerciseArithmetic()
    {
        int checksum = 0;
        for (int index = 0; index < 4096; index++)
        {
            byte left = (byte)(index & 0xFF);
            byte right = (byte)((index % 255) + 1);

            // Consume every result so the calls contribute to the returned value.
            checksum += Gf256.Add(left, right);
            checksum += Gf256.Subtract(left, right);
            checksum += Gf256.Multiply(left, right);
            checksum += Gf256.Inverse(right);
            checksum += Gf256.Divide(left, right);
        }

        return checksum;
    }
}
