using BenchmarkDotNet.Running;

namespace Mrbr.Cryptography.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)) ? 1 : 0;
    }
}
