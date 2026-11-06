using System.Buffers.Binary;
using System.Security.Cryptography;
using Mrbr.Service.KeyManager.Services;

namespace Mrbr.Cryptography.KeyManager;

/// <summary>Shares KeyManager handles or replayed key material using caller-owned buffers.</summary>
/// <remarks>
/// This adapter performs no I/O. Handle recovery requires the original KeyManager
/// source configuration to replay a key. Key-material recovery is independent of
/// that configuration. Shares do not authenticate either form of secret.
/// </remarks>
public static class ShamirKeySharing
{
    /// <summary>The encoded length of a handle, using unsigned little-endian encoding.</summary>
    public const int HandleLength = sizeof(ulong);

    /// <summary>The length of one handle share, including its coordinate.</summary>
    public const int HandleShareLength = HandleLength + 1;

    /// <summary>The largest key accepted by the overload using bounded stack scratch space.</summary>
    public const int MaximumStackKeyLength = 256;

    /// <summary>Creates shares of the eight-byte handle, without replaying the key.</summary>
    /// <returns>The number of packed share bytes written.</returns>
    public static int CreateHandleShares(ulong keyHandle, int threshold, int shareCount, Span<byte> destination)
    {
        Span<byte> encoded = stackalloc byte[HandleLength];
        try
        {
            BinaryPrimitives.WriteUInt64LittleEndian(encoded, keyHandle);
            return ShamirSharedSecret.CreateShares(encoded, threshold, shareCount, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    /// <summary>Recovers a handle from complete nine-byte shares in any order.</summary>
    /// <remarks>The original KeyManager source, mask and settings are still needed to replay this handle.</remarks>
    public static ulong ReconstructHandle(ReadOnlySpan<byte> shares, int threshold)
    {
        Span<byte> encoded = stackalloc byte[HandleLength];
        try
        {
            ShamirSharedSecret.Reconstruct(shares, HandleShareLength, threshold, encoded);
            return BinaryPrimitives.ReadUInt64LittleEndian(encoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    /// <summary>Replays and shares a key of 1..256 bytes using bounded, wiped stack scratch space.</summary>
    /// <remarks>For larger keys use the overload accepting caller-owned scratch space.</remarks>
    /// <returns>The number of packed share bytes written; destination suffix is untouched.</returns>
    public static int CreateKeyShares(IKeyService keyService, ulong keyHandle, int keyLength,
        int threshold, int shareCount, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keyLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(keyLength, MaximumStackKeyLength);
        Span<byte> workspace = stackalloc byte[keyLength];
        return CreateKeyShares(keyService, keyHandle, keyLength, threshold, shareCount, destination, workspace);
    }

    /// <summary>Replays and shares a key of 1..65535 bytes using caller-owned scratch space.</summary>
    /// <param name="keyService">The service holding the original source configuration.</param>
    /// <param name="keyHandle">An existing replayable key handle.</param>
    /// <param name="keyLength">The original key length, required especially for Matrix handles.</param>
    /// <param name="threshold">Required share count, between 2 and shareCount.</param>
    /// <param name="shareCount">Number of shares, between 2 and 255.</param>
    /// <param name="destination">Packed share output, disjoint from the entire workspace.</param>
    /// <param name="keyWorkspace">At least keyLength bytes. The used prefix is wiped even if replay fails; suffix is untouched.</param>
    /// <returns>The number of packed share bytes written.</returns>
    /// <remarks>Parameter validation precedes key replay or buffer changes. Replay failures leave destination unchanged.</remarks>
    public static int CreateKeyShares(IKeyService keyService, ulong keyHandle, int keyLength,
        int threshold, int shareCount, Span<byte> destination, Span<byte> keyWorkspace)
    {
        ArgumentNullException.ThrowIfNull(keyService);
        ValidateKeyLength(keyLength);
        int required = ShamirSharedSecret.GetSharesLength(keyLength, shareCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, shareCount);
        if (destination.Length < required)
            throw new ArgumentException("Destination is too small for all shares.", nameof(destination));
        if (keyWorkspace.Length < keyLength)
            throw new ArgumentException("Workspace is too small for the key.", nameof(keyWorkspace));
        if (keyWorkspace.Overlaps(destination))
            throw new ArgumentException("Workspace and destination must not overlap.", nameof(keyWorkspace));

        Span<byte> key = keyWorkspace[..keyLength];
        try
        {
            keyService.GetKey(keyHandle, key);
            return ShamirSharedSecret.CreateShares(key, threshold, shareCount, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Recovers key bytes into the caller's destination without accessing KeyManager.</summary>
    /// <remarks>The caller owns the recovered secret and must wipe it when finished. Destination suffix is untouched.</remarks>
    /// <returns>The original key length.</returns>
    public static int ReconstructKey(ReadOnlySpan<byte> shares, int keyLength, int threshold, Span<byte> destination)
    {
        ValidateKeyLength(keyLength);
        return ShamirSharedSecret.Reconstruct(shares, ShamirSharedSecret.GetShareLength(keyLength), threshold, destination);
    }

    private static void ValidateKeyLength(int keyLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keyLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(keyLength, ushort.MaxValue);
    }
}
