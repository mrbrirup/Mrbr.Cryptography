```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 44.02 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-LJPAKF : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  Arguments=/p:ComparisonTargetFramework=net10.0,/p:ComparisonUseFrozenAssembly=true  IterationCount=12  
IterationTime=150ms  LaunchCount=1  UnrollFactor=1  
WarmupCount=15  

```
| Method                    | Scenario  | Mean          | Error         | StdDev        | Op/s       | Gen0      | Gen1    | Allocated   |
|-------------------------- |---------- |--------------:|--------------:|--------------:|-----------:|----------:|--------:|------------:|
| **UpstreamBigCreate**         | **128B_3of5** |      **25.75 μs** |      **0.789 μs** |      **0.616 μs** | **38,833.659** |    **4.6091** |  **1.6461** |    **29.12 KB** |
| UpstreamSecureCreate      | 128B_3of5 |   2,409.59 μs |    194.606 μs |    151.936 μs |    415.009 |         - |       - |    20.82 KB |
| UpstreamBigReconstruct    | 128B_3of5 |      72.75 μs |      1.615 μs |      1.261 μs | 13,745.207 |   10.3529 |  5.1765 |     63.8 KB |
| UpstreamSecureReconstruct | 128B_3of5 | 760,220.30 μs | 25,227.168 μs | 19,695.714 μs |      1.315 | 3000.0000 |       - | 19232.81 KB |
| **UpstreamBigCreate**         | **32B_3of5**  |      **20.74 μs** |      **1.824 μs** |      **1.424 μs** | **48,225.828** |    **3.5922** |  **3.4684** |    **22.44 KB** |
| UpstreamSecureCreate      | 32B_3of5  |     687.43 μs |      7.719 μs |      4.593 μs |  1,454.702 |         - |       - |     20.7 KB |
| UpstreamBigReconstruct    | 32B_3of5  |      51.13 μs |      1.125 μs |      0.878 μs | 19,556.904 |    8.6869 |  8.3528 |    53.67 KB |
| UpstreamSecureReconstruct | 32B_3of5  | 187,572.91 μs |  9,923.969 μs |  7,747.982 μs |      5.331 | 1250.0000 |       - |  7886.78 KB |
| **UpstreamBigCreate**         | **32B_9of9**  |      **62.87 μs** |      **0.956 μs** |      **0.691 μs** | **15,904.620** |   **15.0250** |  **1.6694** |    **94.07 KB** |
| UpstreamSecureCreate      | 32B_9of9  |   2,794.53 μs |    259.714 μs |    202.768 μs |    357.842 |         - |       - |    80.85 KB |
| UpstreamBigReconstruct    | 32B_9of9  |     276.75 μs |      9.828 μs |      7.673 μs |  3,613.432 |   54.9683 | 25.3700 |   343.25 KB |
| UpstreamSecureReconstruct | 32B_9of9  | 462,512.15 μs |  5,291.629 μs |  3,148.964 μs |      2.162 | 3000.0000 |       - |  19836.8 KB |
