```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 44.49 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-KBIGZM : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  Arguments=/p:ComparisonTargetFramework=netstandard2.0,/p:ComparisonUseFrozenAssembly=true  IterationCount=12  
IterationTime=150ms  LaunchCount=1  UnrollFactor=1  
WarmupCount=15  

```
| Method                    | Scenario  | Mean          | Error         | StdDev        | Op/s       | Gen0      | Gen1    | Allocated   |
|-------------------------- |---------- |--------------:|--------------:|--------------:|-----------:|----------:|--------:|------------:|
| **UpstreamBigCreate**         | **128B_3of5** |      **25.75 μs** |      **0.665 μs** |      **0.520 μs** | **38,838.177** |    **4.7393** |  **1.5234** |    **29.68 KB** |
| UpstreamSecureCreate      | 128B_3of5 |   2,353.39 μs |     27.231 μs |     19.690 μs |    424.918 |         - |       - |    20.85 KB |
| UpstreamBigReconstruct    | 128B_3of5 |      73.22 μs |      1.728 μs |      1.349 μs | 13,656.939 |   10.2790 |  4.8948 |    63.84 KB |
| UpstreamSecureReconstruct | 128B_3of5 | 735,439.65 μs |  6,938.513 μs |  5,017.000 μs |      1.360 | 3000.0000 |       - | 19232.84 KB |
| **UpstreamBigCreate**         | **32B_3of5**  |      **19.48 μs** |      **0.803 μs** |      **0.627 μs** | **51,330.981** |    **3.6933** |  **3.5660** |    **22.72 KB** |
| UpstreamSecureCreate      | 32B_3of5  |     701.86 μs |      6.914 μs |      4.999 μs |  1,424.789 |         - |       - |    20.73 KB |
| UpstreamBigReconstruct    | 32B_3of5  |      50.51 μs |      1.208 μs |      0.799 μs | 19,797.728 |    8.4329 |  8.0815 |     53.7 KB |
| UpstreamSecureReconstruct | 32B_3of5  | 180,101.15 μs |  1,103.909 μs |    730.167 μs |      5.552 | 1250.0000 |       - |  7886.81 KB |
| **UpstreamBigCreate**         | **32B_9of9**  |      **65.14 μs** |      **2.730 μs** |      **1.974 μs** | **15,351.797** |   **15.2207** |  **0.5074** |    **94.92 KB** |
| UpstreamSecureCreate      | 32B_9of9  |   2,506.89 μs |    168.001 μs |    131.164 μs |    398.900 |         - |       - |    80.88 KB |
| UpstreamBigReconstruct    | 32B_9of9  |     274.36 μs |      9.079 μs |      7.088 μs |  3,644.798 |   55.5556 | 26.8817 |   343.67 KB |
| UpstreamSecureReconstruct | 32B_9of9  | 439,528.08 μs | 26,778.279 μs | 20,906.719 μs |      2.275 | 3000.0000 |       - | 19837.25 KB |
