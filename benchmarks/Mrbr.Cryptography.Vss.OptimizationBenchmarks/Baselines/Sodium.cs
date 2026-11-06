// Frozen VSS increment baseline; only namespace/type names changed.
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mrbr.Cryptography.Vss.OptimizationBenchmarks.Baselines;

// All calls operate on fixed-size stack/caller spans through blittable pointers.
// Keep native initialization separate from successful warmed operation counts.
internal static unsafe partial class SodiumBaseline
{
    private const string Library = "libsodium";

    static SodiumBaseline()
    {
        if (Initialize() < 0) throw new System.Security.Cryptography.CryptographicException("libsodium initialization failed.");
    }

    [LibraryImport(Library, EntryPoint = "sodium_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Initialize();

    [LibraryImport(Library, EntryPoint = "sodium_version_string")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* Version();

    [LibraryImport(Library, EntryPoint = "crypto_core_ristretto255_is_valid_point")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int IsValidPoint(byte* point);
    internal static bool PointIsValid(ReadOnlySpan<byte> point)
    {
        fixed (byte* p = point) return IsValidPoint(p) == 1;
    }

    [LibraryImport(Library, EntryPoint = "crypto_core_ristretto255_from_hash")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int FromHash(byte* result, byte* hash);
    internal static void PointFromHash(Span<byte> result, ReadOnlySpan<byte> hash)
    {
        fixed (byte* r = result, h = hash) _ = FromHash(r, h);
    }

    [LibraryImport(Library, EntryPoint = "crypto_core_ristretto255_add")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int AddPoints(byte* result, byte* left, byte* right);
    internal static void PointAdd(Span<byte> result, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        fixed (byte* r = result, l = left, q = right)
            if (AddPoints(r, l, q) != 0) throw new System.Security.Cryptography.CryptographicException("Invalid internal RistrettoBaseline point.");
    }

    [LibraryImport(Library, EntryPoint = "crypto_scalarmult_ristretto255_base")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int MultiplyBase(byte* result, byte* scalar);
    internal static int BaseMultiply(Span<byte> result, ReadOnlySpan<byte> scalar)
    {
        fixed (byte* r = result, s = scalar) return MultiplyBase(r, s);
    }

    [LibraryImport(Library, EntryPoint = "crypto_scalarmult_ristretto255")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int MultiplyPoint(byte* result, byte* scalar, byte* point);
    internal static int PointMultiply(Span<byte> result, ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> point)
    {
        fixed (byte* r = result, s = scalar, p = point) return MultiplyPoint(r, s, p);
    }

    [LibraryImport(Library, EntryPoint = "crypto_core_ristretto255_scalar_add")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void AddScalars(byte* result, byte* left, byte* right);
    internal static void ScalarAdd(Span<byte> result, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        fixed (byte* r = result, l = left, q = right) AddScalars(r, l, q);
    }

    [LibraryImport(Library, EntryPoint = "crypto_core_ristretto255_scalar_sub")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void SubtractScalars(byte* result, byte* left, byte* right);
    internal static void ScalarSubtract(Span<byte> result, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        fixed (byte* r = result, l = left, q = right) SubtractScalars(r, l, q);
    }

    [LibraryImport(Library, EntryPoint = "crypto_core_ristretto255_scalar_mul")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void MultiplyScalars(byte* result, byte* left, byte* right);
    internal static void ScalarMultiply(Span<byte> result, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        fixed (byte* r = result, l = left, q = right) MultiplyScalars(r, l, q);
    }

    [LibraryImport(Library, EntryPoint = "crypto_core_ristretto255_scalar_invert")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int InvertScalar(byte* result, byte* scalar);
    internal static void ScalarInverse(Span<byte> result, ReadOnlySpan<byte> scalar)
    {
        fixed (byte* r = result, s = scalar)
            if (InvertScalar(r, s) != 0) throw new System.Security.Cryptography.CryptographicException("Invalid internal scalar inverse.");
    }

    [LibraryImport(Library, EntryPoint = "crypto_hash_sha256")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Sha256(byte* result, byte* data, ulong length);
    internal static void Hash256(Span<byte> result, ReadOnlySpan<byte> data)
    {
        fixed (byte* r = result, p = data) _ = Sha256(r, p, (ulong)data.Length);
    }

    [LibraryImport(Library, EntryPoint = "crypto_hash_sha512")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Sha512(byte* result, byte* data, ulong length);
    internal static void Hash512(Span<byte> result, ReadOnlySpan<byte> data)
    {
        fixed (byte* r = result, p = data) _ = Sha512(r, p, (ulong)data.Length);
    }
}
