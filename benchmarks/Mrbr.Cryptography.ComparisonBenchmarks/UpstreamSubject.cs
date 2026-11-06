using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using SecretSharingDotNet.Cryptography;
using SecretSharingDotNet.Cryptography.ShamirsSecretSharing;
using SecretSharingDotNet.Math;
using SecretSharingDotNet.Math.Numerics;
using BigInteger = System.Numerics.BigInteger;

namespace Mrbr.Cryptography.ComparisonBenchmarks;

// Public API adapter only. No external arithmetic is copied into our implementation.
public sealed class UpstreamSubject : IDisposable
{
    private readonly byte[] source;
    private readonly int threshold, count;
    private readonly SecretSplitter<BigInteger> bigSplitter = new();
    private readonly SecretReconstructor<BigInteger> bigReconstructor = new(new ExtendedEuclideanAlgorithm<BigInteger>());
    private readonly SecretSplitter<SecureBigInteger> secureSplitter = new();
    private readonly FixedIterationSecretReconstructor<SecureBigInteger> secureReconstructor = new();
    private readonly Shares<BigInteger> bigSelected;
    private readonly Shares<SecureBigInteger> secureSelected;

    public int BigFieldExponent => bigSplitter.SecurityLevel;
    public int SecureFieldExponent => secureSplitter.SecurityLevel;

    public UpstreamSubject(byte[] source, int threshold, int count)
    {
        this.source = source; this.threshold = threshold; this.count = count;
        using var bigSecret = new Secret<BigInteger>(source);
        using var bigAll = bigSplitter.MakeShares(threshold, count, bigSecret);
        bigSelected = Select(bigAll, threshold);
        using var secureSecret = new Secret<SecureBigInteger>(source);
        using var secureAll = secureSplitter.MakeShares(threshold, count, secureSecret);
        secureSelected = Select(secureAll, threshold);
    }

    private static Shares<T> Select<T>(Shares<T> all, int threshold)
    {
        var selected = new Share<T>[threshold];
        for (int i = 0; i < threshold; i++)
        {
            // Parse a deep copy outside measurement, retaining recorded field metadata.
            using var text = all[all.Count - i - 1].ToCharArray(false, false, ShareFormat.Extended);
            selected[i] = new Share<T>(text);
        }
        return selected;
    }

    public int CreateBig()
    {
        using var secret = new Secret<BigInteger>(source);
        using var shares = bigSplitter.MakeShares(threshold, count, secret);
        return shares.Count;
    }

    public int CreateSecure()
    {
        using var secret = new Secret<SecureBigInteger>(source);
        using var shares = secureSplitter.MakeShares(threshold, count, secret);
        return shares.Count;
    }

    public int ReconstructBig(Span<byte> destination)
    {
        using var secret = bigReconstructor.Reconstruction(bigSelected);
        using var bytes = secret.ToByteArray();
        bytes.PoolArray.AsSpan(0, bytes.Length).CopyTo(destination);
        return bytes.Length;
    }

    public int ReconstructSecure(Span<byte> destination)
    {
        using var secret = secureReconstructor.Reconstruction(secureSelected);
        using var bytes = secret.ToByteArray();
        bytes.PoolArray.AsSpan(0, bytes.Length).CopyTo(destination);
        return bytes.Length;
    }

    public static string LoadedTarget => typeof(SecretSplitter<>).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName;

    public static void Describe()
    {
        Assembly assembly = typeof(SecretSplitter<>).Assembly;
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
        Console.WriteLine($"SUBJECT target={LoadedTarget}; sha256={hash}; runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
    }

    public void Dispose()
    {
        bigSelected.Dispose(); secureSelected.Dispose();
        bigSplitter.Dispose(); secureSplitter.Dispose(); bigReconstructor.Dispose(); secureReconstructor.Dispose();
    }
}
