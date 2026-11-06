using BenchmarkDotNet.Running;
using Mrbr.Cryptography.Vss.OptimizationBenchmarks;

if (args.Length == 1 && args[0] == "--profile")
{
    NativeProfile.Run();
    return 0;
}
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)) ? 1 : 0;
