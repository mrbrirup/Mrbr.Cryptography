using System.Buffers.Binary;
using System.Security.Cryptography;
using Mrbr.Cryptography.Vss.Authentication;
using Mrbr.Service.KeyManager.Services;

namespace Mrbr.Cryptography.Vss.KeyManager;

/// <summary>Replays and shares AES keys with signed Pedersen commitments, and recovers them independently of KeyManager.</summary>
/// <remarks>
/// Supports 16-, 24- and 32-byte AES keys. No I/O, source configuration, encryption, decryption or trust storage
/// is performed. All storage is caller owned. Warm successful and false-return operations allocate no managed
/// memory with the existing span-based KeyService. Inputs must remain stable throughout each call.
/// </remarks>
public static class AuthenticatedKeySharing
{
    /// <summary>Context prefix bytes: protocol domain, original handle, key length and application-context length.</summary>
    public const int ContextHeaderLength = 48;

    private static ReadOnlySpan<byte> Domain => "Mrbr.Cryptography.Vss.KeyManager"u8;

    /// <summary>Returns the signed context length for arbitrary public application metadata.</summary>
    public static int GetContextLength(int applicationContextLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(applicationContextLength);
        return checked(ContextHeaderLength + applicationContextLength);
    }

    /// <summary>Returns the complete authenticated packet size for an AES key and VSS threshold.</summary>
    public static int GetPacketLength(int keyLength, int threshold, int applicationContextLength)
    {
        ValidateKeyLength(keyLength);
        return AuthenticatedCommitmentPacket.GetPacketLength(PedersenVss.GetCommitmentsLength(keyLength, threshold), GetContextLength(applicationContextLength));
    }

    /// <summary>Returns creation scratch size: replayed key, inner commitments and signed context.</summary>
    public static int GetCreationWorkspaceLength(int keyLength, int threshold, int applicationContextLength)
    {
        ValidateKeyLength(keyLength);
        return checked(keyLength + PedersenVss.GetCommitmentsLength(keyLength, threshold) + GetContextLength(applicationContextLength));
    }

    /// <summary>Writes a canonical public context binding the original handle, AES key length and application metadata.</summary>
    /// <remarks>Input must be disjoint from the full destination. Leaves suffix untouched; contains no key bytes.</remarks>
    public static int WriteContext(ulong originalHandle, int keyLength, ReadOnlySpan<byte> applicationContext, Span<byte> destination)
    {
        ValidateKeyLength(keyLength);
        int length = GetContextLength(applicationContext.Length);
        if (destination.Length < length) throw new ArgumentException("Context destination is too small.", nameof(destination));
        if (applicationContext.Overlaps(destination)) throw new ArgumentException("Context input and destination must be disjoint.", nameof(destination));
        WriteContextCore(originalHandle, keyLength, applicationContext, destination[..length]);
        return length;
    }

    /// <summary>Replays an AES key, creates fresh Pedersen shares and signs the commitment envelope.</summary>
    /// <returns>Number of packed share bytes written; packet size is GetPacketLength.</returns>
    /// <remarks>
    /// Parameter validation precedes replay/writes. Both full outputs and full workspace must be mutually
    /// disjoint and disjoint from signingSeed/applicationContext. Used workspace is always wiped after replay
    /// starts, including replay failure. Replay failure leaves outputs unchanged. Failures after share creation
    /// starts clear both used output prefixes. All suffixes are untouched. KeyService controls replay semantics.
    /// </remarks>
    public static int CreateKeyShares(IKeyService keyService, ulong originalHandle, int keyLength, int threshold, int shareCount,
        ReadOnlySpan<byte> applicationContext, ulong generation, ReadOnlySpan<byte> signingSeed,
        Span<byte> sharesDestination, Span<byte> packetDestination, Span<byte> workspace)
    {
        ArgumentNullException.ThrowIfNull(keyService);
        ValidateKeyLength(keyLength);
        int sharesLength = PedersenVss.GetSharesLength(keyLength, shareCount);
        int commitmentsLength = PedersenVss.GetCommitmentsLength(keyLength, threshold);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, shareCount);
        int contextLength = GetContextLength(applicationContext.Length);
        int packetLength = AuthenticatedCommitmentPacket.GetPacketLength(commitmentsLength, contextLength);
        int workspaceLength = checked(keyLength + commitmentsLength + contextLength);
        if (signingSeed.Length != AuthenticatedCommitmentPacket.SigningSeedLength) throw new ArgumentException("Signing seed must be exactly 32 bytes.", nameof(signingSeed));
        if (sharesDestination.Length < sharesLength) throw new ArgumentException("Share destination is too small.", nameof(sharesDestination));
        if (packetDestination.Length < packetLength) throw new ArgumentException("Packet destination is too small.", nameof(packetDestination));
        if (workspace.Length < workspaceLength) throw new ArgumentException("Creation workspace is too small.", nameof(workspace));
        if (sharesDestination.Overlaps(packetDestination) || sharesDestination.Overlaps(workspace) || packetDestination.Overlaps(workspace)
            || applicationContext.Overlaps(sharesDestination) || applicationContext.Overlaps(packetDestination) || applicationContext.Overlaps(workspace)
            || signingSeed.Overlaps(sharesDestination) || signingSeed.Overlaps(packetDestination) || signingSeed.Overlaps(workspace))
            throw new ArgumentException("Full outputs/workspace must be disjoint from each other and all inputs.");

        Span<byte> usedWorkspace = workspace[..workspaceLength];
        Span<byte> key = usedWorkspace[..keyLength], commitments = usedWorkspace.Slice(keyLength, commitmentsLength), context = usedWorkspace[(keyLength + commitmentsLength)..];
        bool outputsStarted = false;
        try
        {
            keyService.GetKey(originalHandle, key);
            WriteContextCore(originalHandle, keyLength, applicationContext, context);
            outputsStarted = true;
            PedersenVss.CreateShares(key, threshold, shareCount, sharesDestination, commitments);
            AuthenticatedCommitmentPacket.Create(commitments, context, generation, signingSeed, packetDestination);
            return sharesLength;
        }
        catch
        {
            if (outputsStarted)
            {
                CryptographicOperations.ZeroMemory(sharesDestination[..sharesLength]);
                CryptographicOperations.ZeroMemory(packetDestination[..packetLength]);
            }
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(usedWorkspace); }
    }

    /// <summary>Authenticates the expected key identity, generation and dealer, verifies all shares and recovers the AES key.</summary>
    /// <remarks>
    /// No KeyManager access. Context workspace must hold GetContextLength bytes. False leaves the key destination
    /// unchanged and bytesWritten zero. Used workspace is wiped on success/failure; suffixes are untouched.
    /// The full destination/workspace must be disjoint from each other and every input. Caller errors throw
    /// before writes. Caller must wipe the recovered key after importing it into a decryptor or finishing use.
    /// </remarks>
    public static bool TryReconstructKey(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet, ulong expectedOriginalHandle, int keyLength,
        ReadOnlySpan<byte> expectedApplicationContext, ulong expectedGeneration, ReadOnlySpan<byte> trustedPublicKey,
        Span<byte> destination, Span<byte> contextWorkspace, out int bytesWritten)
    {
        bytesWritten = 0;
        ValidateKeyLength(keyLength);
        int contextLength = GetContextLength(expectedApplicationContext.Length);
        if (trustedPublicKey.Length != AuthenticatedCommitmentPacket.PublicKeyLength) throw new ArgumentException("Trusted public key must be exactly 32 bytes.", nameof(trustedPublicKey));
        if (destination.Length < keyLength) throw new ArgumentException("Key destination is too small.", nameof(destination));
        if (contextWorkspace.Length < contextLength) throw new ArgumentException("Context workspace is too small.", nameof(contextWorkspace));
        if (destination.Overlaps(contextWorkspace) || shares.Overlaps(destination) || packet.Overlaps(destination)
            || expectedApplicationContext.Overlaps(destination) || trustedPublicKey.Overlaps(destination)
            || shares.Overlaps(contextWorkspace) || packet.Overlaps(contextWorkspace)
            || expectedApplicationContext.Overlaps(contextWorkspace) || trustedPublicKey.Overlaps(contextWorkspace))
            throw new ArgumentException("Full destination/workspace must be disjoint from each other and all inputs.");

        Span<byte> context = contextWorkspace[..contextLength];
        try
        {
            WriteContextCore(expectedOriginalHandle, keyLength, expectedApplicationContext, context);
            if (!AuthenticatedCommitmentPacket.TryVerify(packet, context, expectedGeneration, trustedPublicKey, out var commitments)) return false;
            // Check the signed inner length before VSS can write any destination bytes.
            if (!PedersenVss.TryReadParameters(commitments, out int actualKeyLength, out _, out _) || actualKeyLength != keyLength) return false;
            return PedersenVss.TryReconstruct(shares, commitments, destination, out bytesWritten);
        }
        finally { CryptographicOperations.ZeroMemory(context); }
    }

    /// <summary>Recovers the authenticated AES key, throwing CryptographicException on untrusted-data failure.</summary>
    public static int ReconstructKey(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet, ulong expectedOriginalHandle, int keyLength,
        ReadOnlySpan<byte> expectedApplicationContext, ulong expectedGeneration, ReadOnlySpan<byte> trustedPublicKey,
        Span<byte> destination, Span<byte> contextWorkspace)
    {
        if (!TryReconstructKey(shares, packet, expectedOriginalHandle, keyLength, expectedApplicationContext, expectedGeneration,
            trustedPublicKey, destination, contextWorkspace, out int written))
            throw new CryptographicException("Authenticated AES key recovery failed.");
        return written;
    }

    private static void WriteContextCore(ulong originalHandle, int keyLength, ReadOnlySpan<byte> applicationContext, Span<byte> destination)
    {
        Domain.CopyTo(destination);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[32..], originalHandle);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[40..], (uint)keyLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[44..], (uint)applicationContext.Length);
        applicationContext.CopyTo(destination[ContextHeaderLength..]);
    }

    private static void ValidateKeyLength(int keyLength)
    {
        if (keyLength is not (16 or 24 or 32)) throw new ArgumentOutOfRangeException(nameof(keyLength), "AES key length must be 16, 24 or 32 bytes.");
    }
}
