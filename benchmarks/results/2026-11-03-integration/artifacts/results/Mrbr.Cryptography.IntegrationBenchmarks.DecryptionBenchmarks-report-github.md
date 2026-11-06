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
| Method                         | KeyLength | Mean       | Error     | StdDev    | Op/s        | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |---------- |-----------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| **ExistingHandleDecrypt**          | **16**        | **1,106.1 ns** |  **49.38 ns** |  **46.19 ns** |   **904,061.6** |  **1.00** |    **0.00** |      **-** |      **40 B** |        **1.00** |
| ReusedRawKeyDecrypt            | 16        |   603.5 ns |   8.71 ns |   7.28 ns | 1,657,058.4 |  0.55 |    0.02 |      - |         - |        0.00 |
| InitializeRawKeyAndDecrypt     | 16        | 1,260.5 ns |  86.84 ns |  76.98 ns |   793,346.9 |  1.14 |    0.08 | 0.0162 |     112 B |        2.80 |
| RecoverHandleAndDecrypt        | 16        | 1,969.4 ns | 179.76 ns | 168.15 ns |   507,766.8 |  1.78 |    0.16 |      - |      40 B |        1.00 |
| RecoverKeyInitializeAndDecrypt | 16        | 1,793.3 ns |  23.82 ns |  18.59 ns |   557,623.6 |  1.62 |    0.07 | 0.0119 |     112 B |        2.80 |
|                                |           |            |           |           |             |       |         |        |           |             |
| **ExistingHandleDecrypt**          | **24**        | **1,142.2 ns** |  **94.45 ns** |  **88.35 ns** |   **875,500.8** |  **1.00** |    **0.00** |      **-** |      **40 B** |        **1.00** |
| ReusedRawKeyDecrypt            | 24        |   679.6 ns |  45.32 ns |  42.39 ns | 1,471,495.9 |  0.60 |    0.06 |      - |         - |        0.00 |
| InitializeRawKeyAndDecrypt     | 24        | 1,450.2 ns | 108.81 ns | 101.78 ns |   689,578.1 |  1.28 |    0.13 | 0.0171 |     112 B |        2.80 |
| RecoverHandleAndDecrypt        | 24        | 2,022.3 ns |  70.74 ns |  59.07 ns |   494,482.5 |  1.78 |    0.14 |      - |      40 B |        1.00 |
| RecoverKeyInitializeAndDecrypt | 24        | 2,262.0 ns | 117.98 ns | 110.35 ns |   442,093.1 |  1.99 |    0.17 | 0.0138 |     112 B |        2.80 |
|                                |           |            |           |           |             |       |         |        |           |             |
| **ExistingHandleDecrypt**          | **32**        | **1,231.4 ns** | **108.60 ns** | **101.59 ns** |   **812,084.3** |  **1.00** |    **0.00** |      **-** |      **40 B** |        **1.00** |
| ReusedRawKeyDecrypt            | 32        |   701.0 ns |  41.12 ns |  38.46 ns | 1,426,631.5 |  0.57 |    0.06 |      - |         - |        0.00 |
| InitializeRawKeyAndDecrypt     | 32        | 1,375.2 ns | 124.83 ns | 116.77 ns |   727,178.0 |  1.12 |    0.13 | 0.0171 |     112 B |        2.80 |
| RecoverHandleAndDecrypt        | 32        | 2,031.4 ns | 120.48 ns | 112.70 ns |   492,264.2 |  1.66 |    0.16 |      - |      40 B |        1.00 |
| RecoverKeyInitializeAndDecrypt | 32        | 2,131.7 ns | 200.33 ns | 187.39 ns |   469,118.0 |  1.74 |    0.20 | 0.0148 |     112 B |        2.80 |
