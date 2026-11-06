using BenchmarkDotNet.Running;
using Mrbr.Cryptography.ComparisonBenchmarks;

if (args is ["--verify"])
{
    ComparisonBenchmarks.ValidateExpectedTarget();
    int count = 0;
    foreach (int length in new[] { 1, 8, 16, 24, 32, 128 })
    {
        foreach (int threshold in new[] { 2, 3, 5, 9 })
        {
            byte[] input = new byte[length];
            for (int pattern = 0; pattern < 3; pattern++)
            {
                input.AsSpan().Fill(pattern == 1 ? (byte)255 : (byte)0);
                if (pattern == 2) for (int i = 0; i < length; i++) input[i] = (byte)((31 * i + 19) & 255);
                using var subject = new UpstreamSubject(input, threshold, Math.Max(5, threshold));
                byte[] output = new byte[length];
                if (subject.ReconstructBig(output) != length || !input.AsSpan().SequenceEqual(output)) throw new InvalidOperationException("BigInteger correctness failure.");
                output.AsSpan().Clear();
                if (subject.ReconstructSecure(output) != length || !input.AsSpan().SequenceEqual(output)) throw new InvalidOperationException("SecureBigInteger correctness failure.");
                count++;
            }
        }
    }
    UpstreamSubject.Describe();
    Console.WriteLine($"Verified {count} input/quorum combinations on both upstream backends.");
    return 0;
}
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)) ? 1 : 0;
