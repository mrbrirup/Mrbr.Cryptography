using System.Security.Cryptography;
using System.Text;
using Mrbr.Cryptography;
using Mrbr.Cryptography.Vss;
using Mrbr.Cryptography.Vss.Authentication;

const string shortText = "Hello, Shamir!";
const string loremIpsum = """
    Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor
    incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis
    nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.

    Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore
    eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt
    in culpa qui officia deserunt mollit anim id est laborum.
    """;

RunShamirExample("Short text", shortText);
RunShamirExample("Longer lorem ipsum", loremIpsum);
RunPedersenExample("Short text", shortText);
RunPedersenExample("Longer lorem ipsum", loremIpsum);
RunAuthenticatedPedersenExample("Short text", shortText);
RunAuthenticatedPedersenExample("Longer lorem ipsum", loremIpsum);

Console.WriteLine("Press Enter to close...");
Console.ReadLine();

static void RunShamirExample(string title, string text)
{
    const int threshold = 2;
    const int shareCount = 3;

    byte[] secret = Encoding.UTF8.GetBytes(text);
    int shareLength = ShamirSharedSecret.GetShareLength(secret.Length);
    byte[] shares = new byte[ShamirSharedSecret.GetSharesLength(secret.Length, shareCount)];
    byte[] selectedShares = new byte[threshold * shareLength];
    byte[] recovered = new byte[secret.Length];

    try
    {
        Console.WriteLine($"=== Shamir - {title}: 2 of 3 shares required ===");
        Console.WriteLine($"Original: {text}");
        Console.WriteLine();

        ShamirSharedSecret.CreateShares(secret, threshold, shareCount, shares);

        ShowPair(1, 2);
        ShowPair(1, 3);
        ShowPair(2, 3);

        CryptographicOperations.ZeroMemory(recovered);
        try
        {
            // Keep the original threshold: one share cannot reconstruct the secret.
            ShamirSharedSecret.Reconstruct(shares.AsSpan(0, shareLength), shareLength, threshold, recovered);
            throw new InvalidOperationException("Reconstruction unexpectedly accepted one share.");
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine($"Shares: 1 - Failed as expected: {exception.Message}");
        }

        Console.WriteLine();
    }
    finally
    {
        CryptographicOperations.ZeroMemory(secret);
        CryptographicOperations.ZeroMemory(shares);
        CryptographicOperations.ZeroMemory(selectedShares);
        CryptographicOperations.ZeroMemory(recovered);
    }

    void ShowPair(int first, int second)
    {
        // Each packed share contains its coordinate byte followed by its payload.
        shares.AsSpan((first - 1) * shareLength, shareLength).CopyTo(selectedShares);
        shares.AsSpan((second - 1) * shareLength, shareLength).CopyTo(selectedShares.AsSpan(shareLength));

        int written = ShamirSharedSecret.Reconstruct(selectedShares, shareLength, threshold, recovered);
        Console.WriteLine($"Shares: {first} & {second} - Success");
        Console.WriteLine(Encoding.UTF8.GetString(recovered.AsSpan(0, written)));
        Console.WriteLine();
    }
}

static void RunPedersenExample(string title, string text)
{
    const int threshold = 2;
    const int shareCount = 3;

    byte[] secret = Encoding.UTF8.GetBytes(text);
    int shareLength = PedersenVss.GetShareLength(secret.Length);
    byte[] shares = new byte[PedersenVss.GetSharesLength(secret.Length, shareCount)];
    byte[] commitments = new byte[PedersenVss.GetCommitmentsLength(secret.Length, threshold)];
    byte[] selectedShares = new byte[threshold * shareLength];
    byte[] recovered = new byte[secret.Length];

    try
    {
        Console.WriteLine($"=== Pedersen VSS - {title}: 2 of 3 shares required ===");
        Console.WriteLine($"Original: {text}");
        Console.WriteLine();

        // These commitments are created locally. Recipients must obtain the same
        // authenticated commitment packet when shares are distributed externally.
        PedersenVss.CreateShares(secret, threshold, shareCount, shares, commitments);

        for (int share = 1; share <= shareCount; share++)
        {
            if (!PedersenVss.VerifyShare(shares.AsSpan((share - 1) * shareLength, shareLength), commitments))
                throw new InvalidOperationException($"Original share {share} failed verification.");

            Console.WriteLine($"Share {share} - Verification passed");
        }
        Console.WriteLine();

        ShowPair(1, 2);
        ShowPair(1, 3);
        ShowPair(2, 3);

        CryptographicOperations.ZeroMemory(recovered);
        if (PedersenVss.TryReconstruct(shares.AsSpan(0, shareLength), commitments, recovered, out _))
            throw new InvalidOperationException("Reconstruction unexpectedly accepted one share.");

        Console.WriteLine("Shares: 1 - Failed as expected: at least 2 shares are required.");

        // Copy shares 1 and 2, then change a payload byte in share 1.
        // Keep the original shares, coordinate and commitment digest unchanged.
        shares.AsSpan(0, selectedShares.Length).CopyTo(selectedShares);
        selectedShares[PedersenVss.ShareHeaderLength] ^= 1;

        if (PedersenVss.VerifyShare(selectedShares.AsSpan(0, shareLength), commitments))
            throw new InvalidOperationException("Tampered share unexpectedly passed verification.");

        Console.WriteLine("Share 1 (tampered) - Verification failed as expected");

        if (PedersenVss.TryReconstruct(selectedShares, commitments, recovered, out _))
            throw new InvalidOperationException("Reconstruction unexpectedly accepted a tampered share.");

        Console.WriteLine("Shares: 1 (tampered) & 2 - Reconstruction failed as expected");
        Console.WriteLine();
    }
    finally
    {
        CryptographicOperations.ZeroMemory(secret);
        CryptographicOperations.ZeroMemory(shares);
        CryptographicOperations.ZeroMemory(commitments);
        CryptographicOperations.ZeroMemory(selectedShares);
        CryptographicOperations.ZeroMemory(recovered);
    }

    void ShowPair(int first, int second)
    {
        shares.AsSpan((first - 1) * shareLength, shareLength).CopyTo(selectedShares);
        shares.AsSpan((second - 1) * shareLength, shareLength).CopyTo(selectedShares.AsSpan(shareLength));

        // Reconstruct verifies both shares against the commitments before returning data.
        int written = PedersenVss.Reconstruct(selectedShares, commitments, recovered);
        Console.WriteLine($"Shares: {first} & {second} - Success (shares verified)");
        Console.WriteLine(Encoding.UTF8.GetString(recovered.AsSpan(0, written)));
        Console.WriteLine();
    }
}

static void RunAuthenticatedPedersenExample(string title, string text)
{
    const int threshold = 2;
    const int shareCount = 3;
    const ulong generation = 1;

    byte[] secret = Encoding.UTF8.GetBytes(text);
    byte[] context = Encoding.UTF8.GetBytes($"TestConsoleApp/{title}");
    int shareLength = PedersenVss.GetShareLength(secret.Length);
    byte[] shares = new byte[PedersenVss.GetSharesLength(secret.Length, shareCount)];
    byte[] commitments = new byte[PedersenVss.GetCommitmentsLength(secret.Length, threshold)];
    byte[] selectedShares = new byte[threshold * shareLength];
    byte[] recovered = new byte[secret.Length];
    byte[] signingSeed = new byte[AuthenticatedCommitmentPacket.SigningSeedLength];
    byte[] trustedPublicKey = new byte[AuthenticatedCommitmentPacket.PublicKeyLength];
    byte[] otherSigningSeed = new byte[AuthenticatedCommitmentPacket.SigningSeedLength];
    byte[] wrongPublicKey = new byte[AuthenticatedCommitmentPacket.PublicKeyLength];
    byte[] packet = new byte[AuthenticatedCommitmentPacket.GetPacketLength(commitments.Length, context.Length)];
    byte[] tamperedPacket = new byte[packet.Length];

    try
    {
        Console.WriteLine($"=== Ed25519-authenticated Pedersen VSS - {title}: 2 of 3 shares required ===");
        Console.WriteLine($"Original: {text}");
        Console.WriteLine();

        AuthenticatedCommitmentPacket.GenerateSigningSeed(signingSeed);
        AuthenticatedCommitmentPacket.DerivePublicKey(signingSeed, trustedPublicKey);
        AuthenticatedCommitmentPacket.GenerateSigningSeed(otherSigningSeed);
        AuthenticatedCommitmentPacket.DerivePublicKey(otherSigningSeed, wrongPublicKey);

        PedersenVss.CreateShares(secret, threshold, shareCount, shares, commitments);
        AuthenticatedCommitmentPacket.Create(commitments, context, generation, signingSeed, packet);

        // This local demonstration supplies the trusted key, context and generation.
        // Recipients must obtain those expectations independently of received packets.
        if (!AuthenticatedCommitmentPacket.TryVerify(packet, context, generation, trustedPublicKey, out _))
            throw new InvalidOperationException("Original commitment packet failed authentication.");

        Console.WriteLine("Commitment packet - Signature verification passed");
        for (int share = 1; share <= shareCount; share++)
        {
            if (!AuthenticatedCommitmentPacket.VerifyShare(shares.AsSpan((share - 1) * shareLength, shareLength),
                packet, context, generation, trustedPublicKey))
                throw new InvalidOperationException($"Original share {share} failed authenticated verification.");

            Console.WriteLine($"Share {share} - Authenticated verification passed");
        }
        Console.WriteLine();

        ShowPair(1, 2);
        ShowPair(1, 3);
        ShowPair(2, 3);

        CryptographicOperations.ZeroMemory(recovered);
        if (AuthenticatedCommitmentPacket.TryReconstruct(shares.AsSpan(0, shareLength), packet,
            context, generation, trustedPublicKey, recovered, out _))
            throw new InvalidOperationException("Authenticated reconstruction unexpectedly accepted one share.");

        Console.WriteLine("Shares: 1 - Failed as expected: at least 2 shares are required.");

        // Use two valid shares for both authentication failure demonstrations.
        shares.AsSpan(0, selectedShares.Length).CopyTo(selectedShares);
        packet.CopyTo(tamperedPacket, 0);
        // Change a signed commitment byte, retaining the original framing and signature.
        int commitmentOffset = AuthenticatedCommitmentPacket.HeaderLength + context.Length;
        tamperedPacket[commitmentOffset + PedersenVss.CommitmentHeaderLength] ^= 1;

        ShowRejectedPacket("Tampered commitment packet", tamperedPacket, trustedPublicKey);
        ShowRejectedPacket("Wrong public key", packet, wrongPublicKey);
        Console.WriteLine();
    }
    finally
    {
        CryptographicOperations.ZeroMemory(secret);
        CryptographicOperations.ZeroMemory(context);
        CryptographicOperations.ZeroMemory(shares);
        CryptographicOperations.ZeroMemory(commitments);
        CryptographicOperations.ZeroMemory(selectedShares);
        CryptographicOperations.ZeroMemory(recovered);
        CryptographicOperations.ZeroMemory(signingSeed);
        CryptographicOperations.ZeroMemory(trustedPublicKey);
        CryptographicOperations.ZeroMemory(otherSigningSeed);
        CryptographicOperations.ZeroMemory(wrongPublicKey);
        CryptographicOperations.ZeroMemory(packet);
        CryptographicOperations.ZeroMemory(tamperedPacket);
    }

    void ShowPair(int first, int second)
    {
        shares.AsSpan((first - 1) * shareLength, shareLength).CopyTo(selectedShares);
        shares.AsSpan((second - 1) * shareLength, shareLength).CopyTo(selectedShares.AsSpan(shareLength));

        int written = AuthenticatedCommitmentPacket.Reconstruct(selectedShares, packet,
            context, generation, trustedPublicKey, recovered);
        Console.WriteLine($"Shares: {first} & {second} - Success (signature and shares verified)");
        Console.WriteLine(Encoding.UTF8.GetString(recovered.AsSpan(0, written)));
        Console.WriteLine();
    }

    void ShowRejectedPacket(string description, byte[] candidatePacket, byte[] publicKey)
    {
        if (AuthenticatedCommitmentPacket.TryVerify(candidatePacket, context, generation, publicKey, out _))
            throw new InvalidOperationException($"{description} unexpectedly passed signature verification.");

        Console.WriteLine($"{description} - Signature verification failed as expected");

        if (AuthenticatedCommitmentPacket.TryReconstruct(selectedShares, candidatePacket,
            context, generation, publicKey, recovered, out _))
            throw new InvalidOperationException($"{description} unexpectedly allowed reconstruction.");

        Console.WriteLine($"Shares: 1 & 2 ({description}) - Reconstruction failed as expected");
    }
}
