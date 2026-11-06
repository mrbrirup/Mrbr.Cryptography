```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 41.63 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-DZDKHJ : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  Arguments=/p:UseLocalKeyManager=true,/p:ShouldUnsetParentConfigurationAndPlatform=false  IterationCount=15  
IterationTime=150ms  LaunchCount=1  UnrollFactor=1  
WarmupCount=20  

```
| Method        | Categories | Mean       | Error    | StdDev   | Op/s        | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |----------- |-----------:|---------:|---------:|------------:|------:|--------:|----------:|------------:|
| CoreCreate    | Create     | 1,142.2 ns | 66.62 ns | 62.31 ns |   875,499.8 |  1.00 |    0.00 |         - |          NA |
| CreateHandle  | Create     | 1,075.9 ns | 22.87 ns | 20.27 ns |   929,449.3 |  0.94 |    0.05 |         - |          NA |
|               |            |            |          |          |             |       |         |           |             |
| CoreRecover   | Recover    |   745.6 ns | 49.96 ns | 46.73 ns | 1,341,167.4 |  1.00 |    0.00 |         - |          NA |
| RecoverHandle | Recover    |   748.4 ns | 33.92 ns | 30.07 ns | 1,336,259.5 |  1.01 |    0.07 |         - |          NA |
