using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Mrbr.Cryptography.Vss.Authentication.Internal;

// This optional project's binding does not change the VSS backend or its hot paths.
internal static unsafe partial class Ed25519Native
{
    private const string Library = "libsodium";

    static Ed25519Native()
    {
        if (Initialize() < 0) throw new CryptographicException("libsodium initialization failed.");
    }

    [LibraryImport(Library, EntryPoint = "sodium_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Initialize();

    [LibraryImport(Library, EntryPoint = "crypto_sign_seed_keypair")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int SeedKeyPair(byte* publicKey, byte* secretKey, byte* seed);

    internal static void DeriveKeyPair(ReadOnlySpan<byte> seed, Span<byte> publicKey, Span<byte> secretKey)
    {
        fixed (byte* s = seed, p = publicKey, k = secretKey)
            if (SeedKeyPair(p, k, s) != 0) throw new CryptographicException("Ed25519 key derivation failed.");
    }

    [LibraryImport(Library, EntryPoint = "crypto_sign_detached")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int SignDetached(byte* signature, ulong* signatureLength, byte* message, ulong messageLength, byte* secretKey);

    internal static void Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> secretKey, Span<byte> signature)
    {
        fixed (byte* m = message, k = secretKey, s = signature)
            if (SignDetached(s, null, m, (ulong)message.Length, k) != 0)
                throw new CryptographicException("Ed25519 signing failed.");
    }

    [LibraryImport(Library, EntryPoint = "crypto_sign_verify_detached")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int VerifyDetached(byte* signature, byte* message, ulong messageLength, byte* publicKey);

    internal static bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> signature)
    {
        fixed (byte* m = message, p = publicKey, s = signature)
            return VerifyDetached(s, m, (ulong)message.Length, p) == 0;
    }
}
