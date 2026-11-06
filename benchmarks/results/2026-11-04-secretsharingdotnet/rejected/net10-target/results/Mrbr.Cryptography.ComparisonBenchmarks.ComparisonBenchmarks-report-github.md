```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 43.95 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-NMRZNR : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  Arguments=/p:ComparisonTargetFramework=net10.0  IterationCount=12  
IterationTime=150ms  LaunchCount=1  UnrollFactor=1  
WarmupCount=15  

```
| Method                    | Scenario  | Mean | Error | Op/s |
|-------------------------- |---------- |-----:|------:|-----:|
| **UpstreamBigCreate**         | **128B_3of5** |   **NA** |    **NA** |   **NA** |
| UpstreamSecureCreate      | 128B_3of5 |   NA |    NA |   NA |
| UpstreamBigReconstruct    | 128B_3of5 |   NA |    NA |   NA |
| UpstreamSecureReconstruct | 128B_3of5 |   NA |    NA |   NA |
| **UpstreamBigCreate**         | **32B_3of5**  |   **NA** |    **NA** |   **NA** |
| UpstreamSecureCreate      | 32B_3of5  |   NA |    NA |   NA |
| UpstreamBigReconstruct    | 32B_3of5  |   NA |    NA |   NA |
| UpstreamSecureReconstruct | 32B_3of5  |   NA |    NA |   NA |
| **UpstreamBigCreate**         | **32B_9of9**  |   **NA** |    **NA** |   **NA** |
| UpstreamSecureCreate      | 32B_9of9  |   NA |    NA |   NA |
| UpstreamBigReconstruct    | 32B_9of9  |   NA |    NA |   NA |
| UpstreamSecureReconstruct | 32B_9of9  |   NA |    NA |   NA |

Benchmarks with issues:
  ComparisonBenchmarks.UpstreamBigCreate: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=128B_3of5]
  ComparisonBenchmarks.UpstreamSecureCreate: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=128B_3of5]
  ComparisonBenchmarks.UpstreamBigReconstruct: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=128B_3of5]
  ComparisonBenchmarks.UpstreamSecureReconstruct: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=128B_3of5]
  ComparisonBenchmarks.UpstreamBigCreate: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_3of5]
  ComparisonBenchmarks.UpstreamSecureCreate: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_3of5]
  ComparisonBenchmarks.UpstreamBigReconstruct: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_3of5]
  ComparisonBenchmarks.UpstreamSecureReconstruct: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_3of5]
  ComparisonBenchmarks.UpstreamBigCreate: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_9of9]
  ComparisonBenchmarks.UpstreamSecureCreate: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_9of9]
  ComparisonBenchmarks.UpstreamBigReconstruct: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_9of9]
  ComparisonBenchmarks.UpstreamSecureReconstruct: Job-NMRZNR(Arguments=/p:ComparisonTargetFramework=net10.0, IterationCount=12, IterationTime=150ms, LaunchCount=1, UnrollFactor=1, WarmupCount=15) [Scenario=32B_9of9]
