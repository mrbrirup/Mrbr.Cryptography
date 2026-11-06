using System.Buffers.Binary;
using System.Security.Cryptography;
using Mrbr.Cryptography.Vss.Authentication.Internal;

namespace Mrbr.Cryptography.Vss.Authentication;

/// <summary>Authenticates Pedersen commitment packets with pure Ed25519 and explicit application context.</summary>
/// <remarks>
/// No I/O, key storage, trust discovery or replay state is provided. The caller supplies a trusted public key,
/// expected context and exact generation. Context is public. Inputs must remain stable for the entire call.
/// Warm successful calls and untrusted-data false returns allocate no managed memory. Initialization,
/// caller buffers and exceptions are outside that contract. Native allocation traffic is not measured.
/// </remarks>
public static class AuthenticatedCommitmentPacket
{
    /// <summary>Length of an Ed25519 secret signing seed. This is not a libsodium expanded secret key.</summary>
    public const int SigningSeedLength = 32;
    /// <summary>Length of the public key that recipients must obtain through a trusted channel.</summary>
    public const int PublicKeyLength = 32;
    /// <summary>Length of a detached Ed25519 signature.</summary>
    public const int SignatureLength = 64;
    /// <summary>Fixed envelope header length, before variable context and the unchanged commitment packet.</summary>
    public const int HeaderLength = 52;

    private static ReadOnlySpan<byte> Domain => "Mrbr.Cryptography.Vss.AuthPacket"u8;

    /// <summary>Returns the exact envelope size; rejects negative context lengths, short commitment lengths and overflow.</summary>
    public static int GetPacketLength(int commitmentsLength, int contextLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(commitmentsLength, PedersenVss.CommitmentHeaderLength);
        ArgumentOutOfRangeException.ThrowIfNegative(contextLength);
        return checked(HeaderLength + contextLength + commitmentsLength + SignatureLength);
    }

    /// <summary>Generates a fresh signing seed into the first 32 destination bytes; leaves the suffix untouched.</summary>
    /// <remarks>The caller owns and must clear the secret seed. RNG failures clear the used prefix.</remarks>
    public static void GenerateSigningSeed(Span<byte> destination)
    {
        if (destination.Length < SigningSeedLength) throw new ArgumentException("Seed destination is too small.", nameof(destination));
        Span<byte> seed = destination[..SigningSeedLength];
        try { RandomNumberGenerator.Fill(seed); }
        catch { CryptographicOperations.ZeroMemory(seed); throw; }
    }

    /// <summary>Derives the 32-byte public key from an exact 32-byte signing seed.</summary>
    /// <remarks>Full input/output spans must be disjoint. The destination suffix is untouched.</remarks>
    public static void DerivePublicKey(ReadOnlySpan<byte> signingSeed, Span<byte> destination)
    {
        ValidateSeed(signingSeed);
        if (destination.Length < PublicKeyLength) throw new ArgumentException("Public key destination is too small.", nameof(destination));
        if (signingSeed.Overlaps(destination)) throw new ArgumentException("Seed and destination must be disjoint.", nameof(destination));
        Span<byte> secretKey = stackalloc byte[64];
        try { Ed25519Native.DeriveKeyPair(signingSeed, destination[..PublicKeyLength], secretKey); }
        catch { CryptographicOperations.ZeroMemory(destination[..PublicKeyLength]); throw; }
        finally { CryptographicOperations.ZeroMemory(secretKey); }
    }

    /// <summary>Copies and signs a framed Pedersen commitment packet, public context and generation.</summary>
    /// <returns>The envelope bytes written; the destination suffix is untouched.</returns>
    /// <remarks>
    /// All inputs must be disjoint from the full destination span. Parameter errors precede writes;
    /// unexpected backend exceptions clear the used output prefix. The expanded secret key is always wiped.
    /// Checks commitment framing, not group validity or dealer honesty. Context is an arbitrary public byte
    /// sequence; use an unambiguous application encoding. The signature covers every byte except itself.
    /// </remarks>
    public static int Create(ReadOnlySpan<byte> commitments, ReadOnlySpan<byte> context, ulong generation,
        ReadOnlySpan<byte> signingSeed, Span<byte> destination)
    {
        ValidateSeed(signingSeed);
        if (!PedersenVss.TryReadParameters(commitments, out _, out _, out _))
            throw new ArgumentException("Invalid commitment packet framing.", nameof(commitments));
        int length = GetPacketLength(commitments.Length, context.Length);
        if (destination.Length < length) throw new ArgumentException("Packet destination is too small.", nameof(destination));
        if (commitments.Overlaps(destination) || context.Overlaps(destination) || signingSeed.Overlaps(destination))
            throw new ArgumentException("Inputs and full destination must be disjoint.", nameof(destination));

        Span<byte> packet = destination[..length], secretKey = stackalloc byte[64], publicKey = stackalloc byte[PublicKeyLength];
        try
        {
            Ed25519Native.DeriveKeyPair(signingSeed, publicKey, secretKey);
            Domain.CopyTo(packet);
            packet[32] = 1; // Envelope version.
            packet[33] = 1; // Pure Ed25519 over the complete contiguous transcript.
            packet[34] = packet[35] = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(packet[36..], (uint)context.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(packet[40..], (uint)commitments.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(packet[44..], generation);
            context.CopyTo(packet[HeaderLength..]);
            commitments.CopyTo(packet[(HeaderLength + context.Length)..]);
            Ed25519Native.Sign(packet[..^SignatureLength], secretKey, packet[^SignatureLength..]);
            return length;
        }
        catch { CryptographicOperations.ZeroMemory(packet); throw; }
        finally { CryptographicOperations.ZeroMemory(secretKey); }
    }

    /// <summary>Authenticates an envelope against a trusted key, expected public context and exact generation.</summary>
    /// <param name="packet">Exactly one complete envelope, excluding spare capacity.</param>
    /// <param name="expectedContext">Application context expected by this operation; compared byte for byte.</param>
    /// <param name="expectedGeneration">Exact generation expected by the caller's freshness policy.</param>
    /// <param name="trustedPublicKey">An exact 32-byte Ed25519 public key obtained independently of this envelope.</param>
    /// <param name="commitments">On success, a read-only slice of packet; on false, default. Does not copy bytes.</param>
    /// <returns>False for malformed framing, context/generation mismatch or an invalid signature.</returns>
    /// <remarks>
    /// Authenticates origin and bytes, not VSS point validity or agreement between recipients. Use VerifyShare
    /// or TryReconstruct for group validation. The returned slice is valid only while its backing storage remains
    /// alive and unchanged. An old signed packet still verifies if the caller supplies its old generation.
    /// </remarks>
    public static bool TryVerify(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> expectedContext, ulong expectedGeneration,
        ReadOnlySpan<byte> trustedPublicKey, out ReadOnlySpan<byte> commitments)
    {
        ValidatePublicKey(trustedPublicKey);
        commitments = default;
        if (packet.Length < HeaderLength + PedersenVss.CommitmentHeaderLength + SignatureLength) return false;
        if (!packet[..32].SequenceEqual(Domain) || packet[32] != 1 || packet[33] != 1 || packet[34] != 0 || packet[35] != 0) return false;
        uint contextLength = BinaryPrimitives.ReadUInt32LittleEndian(packet[36..]);
        uint commitmentsLength = BinaryPrimitives.ReadUInt32LittleEndian(packet[40..]);
        if ((long)HeaderLength + contextLength + commitmentsLength + SignatureLength != packet.Length) return false;
        if (contextLength != expectedContext.Length || commitmentsLength < PedersenVss.CommitmentHeaderLength) return false;
        if (BinaryPrimitives.ReadUInt64LittleEndian(packet[44..]) != expectedGeneration) return false;
        if (!packet.Slice(HeaderLength, (int)contextLength).SequenceEqual(expectedContext)) return false;
        ReadOnlySpan<byte> inner = packet.Slice(HeaderLength + (int)contextLength, (int)commitmentsLength);
        if (!PedersenVss.TryReadParameters(inner, out _, out _, out _)) return false;
        if (!Ed25519Native.Verify(packet[..^SignatureLength], trustedPublicKey, packet[^SignatureLength..])) return false;
        commitments = inner;
        return true;
    }

    /// <summary>Authenticates the commitment envelope, then verifies one share using PedersenVss.</summary>
    public static bool VerifyShare(ReadOnlySpan<byte> share, ReadOnlySpan<byte> packet, ReadOnlySpan<byte> expectedContext,
        ulong expectedGeneration, ReadOnlySpan<byte> trustedPublicKey) =>
        TryVerify(packet, expectedContext, expectedGeneration, trustedPublicKey, out var commitments) &&
        PedersenVss.VerifyShare(share, commitments);

    /// <summary>Authenticates the envelope, verifies every supplied share and reconstructs the original bytes.</summary>
    /// <remarks>
    /// False returns write zero bytes and leave destination unchanged. Full destination must be disjoint from
    /// every input, including expected context and trusted key. Small destinations and overlap throw before
    /// output writes. Suffixes are untouched. Shares remain confidential and require private distribution.
    /// </remarks>
    public static bool TryReconstruct(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet, ReadOnlySpan<byte> expectedContext,
        ulong expectedGeneration, ReadOnlySpan<byte> trustedPublicKey, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        ValidatePublicKey(trustedPublicKey);
        if (shares.Overlaps(destination) || packet.Overlaps(destination) || expectedContext.Overlaps(destination) || trustedPublicKey.Overlaps(destination))
            throw new ArgumentException("Destination must not overlap any input.", nameof(destination));
        return TryVerify(packet, expectedContext, expectedGeneration, trustedPublicKey, out var commitments) &&
            PedersenVss.TryReconstruct(shares, commitments, destination, out bytesWritten);
    }

    /// <summary>Authenticates and reconstructs, throwing CryptographicException on untrusted-data failure.</summary>
    public static int Reconstruct(ReadOnlySpan<byte> shares, ReadOnlySpan<byte> packet, ReadOnlySpan<byte> expectedContext,
        ulong expectedGeneration, ReadOnlySpan<byte> trustedPublicKey, Span<byte> destination)
    {
        if (!TryReconstruct(shares, packet, expectedContext, expectedGeneration, trustedPublicKey, destination, out int written))
            throw new CryptographicException("Commitment authentication, VSS verification or reconstruction failed.");
        return written;
    }

    private static void ValidateSeed(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != SigningSeedLength) throw new ArgumentException("Signing seed must be exactly 32 bytes.", nameof(seed));
    }

    private static void ValidatePublicKey(ReadOnlySpan<byte> publicKey)
    {
        if (publicKey.Length != PublicKeyLength) throw new ArgumentException("Trusted public key must be exactly 32 bytes.", nameof(publicKey));
    }
}
