using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;

namespace Mrbr.Cryptography.WeightBenchmarks;

public sealed class WeightConfig : ManualConfig
{
    public WeightConfig()
    {
        AddJob(Job.Default.DontEnforcePowerPlan().AsDefault());
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddExporter(JsonExporter.Full);
    }
}
