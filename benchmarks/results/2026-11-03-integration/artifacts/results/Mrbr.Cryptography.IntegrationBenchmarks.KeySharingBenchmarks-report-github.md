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
| Method                 | Categories | Source | KeyLength | Mean       | Error     | StdDev    | Op/s        | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------- |----------- |------- |---------- |-----------:|----------:|----------:|------------:|------:|--------:|----------:|------------:|
| **CoreCreate**             | **Create**     | **0**      | **16**        |   **575.9 ns** |  **33.19 ns** |  **27.72 ns** | **1,736,517.9** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ReplayAndCreate        | Create     | 0      | 16        |   575.0 ns |  39.73 ns |  37.16 ns | 1,738,991.9 |  1.00 |    0.08 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreCreate**             | **Create**     | **0**      | **24**        | **1,501.8 ns** |  **99.39 ns** |  **92.97 ns** |   **665,866.3** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ReplayAndCreate        | Create     | 0      | 24        | 1,572.0 ns | 101.74 ns |  95.17 ns |   636,122.7 |  1.05 |    0.09 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreCreate**             | **Create**     | **0**      | **32**        |   **694.9 ns** |  **47.15 ns** |  **44.11 ns** | **1,439,028.4** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ReplayAndCreate        | Create     | 0      | 32        |   776.8 ns |  65.22 ns |  61.01 ns | 1,287,284.0 |  1.12 |    0.11 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreCreate**             | **Create**     | **1**      | **16**        |   **562.8 ns** |  **50.12 ns** |  **46.88 ns** | **1,776,944.3** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ReplayAndCreate        | Create     | 1      | 16        |   602.3 ns |  26.52 ns |  23.51 ns | 1,660,402.8 |  1.08 |    0.09 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreCreate**             | **Create**     | **1**      | **24**        | **1,472.7 ns** |  **69.19 ns** |  **64.72 ns** |   **679,039.2** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ReplayAndCreate        | Create     | 1      | 24        | 1,604.2 ns | 149.36 ns | 139.72 ns |   623,365.7 |  1.09 |    0.10 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreCreate**             | **Create**     | **1**      | **32**        |   **656.9 ns** |  **30.94 ns** |  **28.94 ns** | **1,522,208.4** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ReplayAndCreate        | Create     | 1      | 32        |   729.9 ns |  26.73 ns |  23.70 ns | 1,370,079.0 |  1.11 |    0.06 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreRecover**            | **Recover**    | **0**      | **16**        |   **497.6 ns** |   **7.42 ns** |   **5.80 ns** | **2,009,579.9** |  **1.00** |    **0.00** |         **-** |          **NA** |
| RecoverKey             | Recover    | 0      | 16        |   530.4 ns |  36.10 ns |  33.76 ns | 1,885,341.0 |  1.07 |    0.07 |         - |          NA |
| RecoverHandleAndReplay | Recover    | 0      | 16        |   779.2 ns |  57.79 ns |  54.05 ns | 1,283,445.2 |  1.57 |    0.11 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreRecover**            | **Recover**    | **0**      | **24**        |   **817.6 ns** |  **65.97 ns** |  **61.71 ns** | **1,223,144.5** |  **1.00** |    **0.00** |         **-** |          **NA** |
| RecoverKey             | Recover    | 0      | 24        |   756.0 ns |  23.15 ns |  19.33 ns | 1,322,820.9 |  0.93 |    0.07 |         - |          NA |
| RecoverHandleAndReplay | Recover    | 0      | 24        |   766.8 ns |  47.96 ns |  44.86 ns | 1,304,126.0 |  0.94 |    0.09 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreRecover**            | **Recover**    | **0**      | **32**        |   **524.2 ns** |  **28.60 ns** |  **26.75 ns** | **1,907,724.5** |  **1.00** |    **0.00** |         **-** |          **NA** |
| RecoverKey             | Recover    | 0      | 32        |   525.9 ns |  25.90 ns |  24.23 ns | 1,901,364.0 |  1.01 |    0.07 |         - |          NA |
| RecoverHandleAndReplay | Recover    | 0      | 32        |   771.4 ns |  33.51 ns |  26.16 ns | 1,296,314.6 |  1.48 |    0.09 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreRecover**            | **Recover**    | **1**      | **16**        |   **498.2 ns** |   **6.20 ns** |   **5.18 ns** | **2,007,153.0** |  **1.00** |    **0.00** |         **-** |          **NA** |
| RecoverKey             | Recover    | 1      | 16        |   524.7 ns |  31.71 ns |  29.66 ns | 1,905,672.8 |  1.05 |    0.06 |         - |          NA |
| RecoverHandleAndReplay | Recover    | 1      | 16        |   772.9 ns |  12.61 ns |  11.18 ns | 1,293,785.7 |  1.55 |    0.03 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreRecover**            | **Recover**    | **1**      | **24**        |   **769.3 ns** |  **43.60 ns** |  **40.78 ns** | **1,299,898.0** |  **1.00** |    **0.00** |         **-** |          **NA** |
| RecoverKey             | Recover    | 1      | 24        |   846.9 ns |  56.05 ns |  52.43 ns | 1,180,766.7 |  1.10 |    0.09 |         - |          NA |
| RecoverHandleAndReplay | Recover    | 1      | 24        |   835.0 ns |  73.96 ns |  69.19 ns | 1,197,594.2 |  1.09 |    0.10 |         - |          NA |
|                        |            |        |           |            |           |           |             |       |         |           |             |
| **CoreRecover**            | **Recover**    | **1**      | **32**        |   **522.5 ns** |  **43.25 ns** |  **40.46 ns** | **1,913,834.7** |  **1.00** |    **0.00** |         **-** |          **NA** |
| RecoverKey             | Recover    | 1      | 32        |   501.3 ns |   7.78 ns |   6.50 ns | 1,994,936.2 |  0.96 |    0.07 |         - |          NA |
| RecoverHandleAndReplay | Recover    | 1      | 32        |   822.3 ns |  38.17 ns |  33.83 ns | 1,216,060.1 |  1.58 |    0.13 |         - |          NA |
