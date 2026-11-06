using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Mrbr.Cryptography.Benchmarks;

public sealed class ThroughputConfig : ManualConfig
{
    public ThroughputConfig()
    {
        AddJob(Job.Default.DontEnforcePowerPlan().AsDefault());
        AddColumn(StatisticColumn.OperationsPerSecond, new ByteThroughputColumn(false), new ByteThroughputColumn(true));
        AddExporter(JsonExporter.Full);
    }

    private sealed class ByteThroughputColumn(bool countShareBytes) : IColumn
    {
        public string Id => ColumnName;
        public string ColumnName => countShareBytes ? "Share MiB/s" : "Secret MiB/s";
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => countShareBytes ? 1 : 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => countShareBytes
            ? "Share bytes generated (creation) or consumed (reconstruction), including coordinate bytes, per second / 1,048,576."
            : "Original secret payload bytes per second / 1,048,576, independent of share count.";

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public bool IsAvailable(Summary summary) => true;
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            var statistics = summary[benchmarkCase]?.ResultStatistics;
            if (statistics is null || statistics.Mean <= 0)
            {
                return "NA";
            }

            if (benchmarkCase.Parameters.Items.Single(parameter =>
                parameter.Name == nameof(ShamirSharedSecretBenchmarks.Scenario)).Value is not SharingScenario scenario)
            {
                return "NA";
            }

            double bytes = scenario.SecretLength;
            if (countShareBytes)
            {
                int count = benchmarkCase.Descriptor.WorkloadMethod.Name == nameof(ShamirSharedSecretBenchmarks.ReconstructThreshold)
                    ? scenario.Threshold
                    : scenario.ShareCount;
                bytes = ((double)scenario.SecretLength + 1) * count;
            }

            double mibPerSecond = bytes * 1_000_000_000 / statistics.Mean / (1024 * 1024);
            return mibPerSecond.ToString("G4", CultureInfo.InvariantCulture);
        }
    }
}
