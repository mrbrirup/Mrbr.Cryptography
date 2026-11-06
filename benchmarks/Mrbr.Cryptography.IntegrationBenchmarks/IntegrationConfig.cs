using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;

namespace Mrbr.Cryptography.IntegrationBenchmarks;

public sealed class IntegrationConfig : ManualConfig
{
    public IntegrationConfig()
    {
        AddJob(Job.Default.DontEnforcePowerPlan()
            .WithArguments([new MsBuildArgument("/p:UseLocalKeyManager=true"),
                new MsBuildArgument("/p:ShouldUnsetParentConfigurationAndPlatform=false")])
            .AsDefault());
        AddColumn(StatisticColumn.OperationsPerSecond, CategoriesColumn.Default);
        AddExporter(JsonExporter.Full);
    }
}
