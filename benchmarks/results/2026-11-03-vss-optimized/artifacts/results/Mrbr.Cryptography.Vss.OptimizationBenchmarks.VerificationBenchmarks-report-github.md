```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 42.65 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-RCBXQR : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  IterationCount=12  IterationTime=120ms  
LaunchCount=1  UnrollFactor=1  WarmupCount=15  

```
| Method               | Scenario           | Mean        | Error       | StdDev      | Op/s     | Allocated |
|--------------------- |------------------- |------------:|------------:|------------:|---------:|----------:|
| **OriginalVerify**       | **1024B_k3_m3_Dense**  | **13,915.7 μs** |   **487.08 μs** |   **380.28 μs** |    **71.86** |         **-** |
| OptimizedVerify      | 1024B_k3_m3_Dense  | 10,557.3 μs |   414.18 μs |   323.37 μs |    94.72 |         - |
| OriginalReconstruct  | 1024B_k3_m3_Dense  | 39,547.2 μs |   601.40 μs |   469.53 μs |    25.29 |         - |
| OptimizedReconstruct | 1024B_k3_m3_Dense  | 25,128.2 μs |   578.32 μs |   451.52 μs |    39.80 |         - |
| **OriginalVerify**       | **32B_k10_m10_Sparse** |  **2,554.5 μs** |   **108.80 μs** |    **84.94 μs** |   **391.46** |         **-** |
| OptimizedVerify      | 32B_k10_m10_Sparse |  2,557.0 μs |   121.70 μs |    95.01 μs |   391.08 |         - |
| OriginalReconstruct  | 32B_k10_m10_Sparse | 24,384.7 μs |   994.48 μs |   776.43 μs |    41.01 |         - |
| OptimizedReconstruct | 32B_k10_m10_Sparse | 22,850.4 μs |   815.91 μs |   589.95 μs |    43.76 |         - |
| **OriginalVerify**       | **32B_k2_m2_Dense**    |    **584.0 μs** |    **11.71 μs** |     **9.14 μs** | **1,712.32** |         **-** |
| OptimizedVerify      | 32B_k2_m2_Dense    |    418.8 μs |     9.41 μs |     6.81 μs | 2,387.50 |         - |
| OriginalReconstruct  | 32B_k2_m2_Dense    |  1,204.3 μs |    63.69 μs |    49.73 μs |   830.33 |         - |
| OptimizedReconstruct | 32B_k2_m2_Dense    |    679.1 μs |    36.06 μs |    28.16 μs | 1,472.48 |         - |
| **OriginalVerify**       | **32B_k2_m2_Sparse**   |    **579.2 μs** |     **6.30 μs** |     **4.17 μs** | **1,726.59** |         **-** |
| OptimizedVerify      | 32B_k2_m2_Sparse   |    569.1 μs |     7.21 μs |     5.22 μs | 1,757.07 |         - |
| OriginalReconstruct  | 32B_k2_m2_Sparse   |  1,177.4 μs |    32.68 μs |    23.63 μs |   849.30 |         - |
| OptimizedReconstruct | 32B_k2_m2_Sparse   |    679.9 μs |    26.88 μs |    20.99 μs | 1,470.76 |         - |
| **OriginalVerify**       | **32B_k3_m3_Dense**    |    **837.4 μs** |    **19.17 μs** |    **14.96 μs** | **1,194.24** |         **-** |
| OptimizedVerify      | 32B_k3_m3_Dense    |    609.0 μs |     7.19 μs |     5.20 μs | 1,642.00 |         - |
| OriginalReconstruct  | 32B_k3_m3_Dense    |  2,441.1 μs |    43.20 μs |    31.24 μs |   409.65 |         - |
| OptimizedReconstruct | 32B_k3_m3_Dense    |    990.9 μs |    20.92 μs |    16.33 μs | 1,009.18 |         - |
| **OriginalVerify**       | **32B_k3_m3_Dense_x1** |    **847.7 μs** |    **26.82 μs** |    **20.94 μs** | **1,179.59** |         **-** |
| OptimizedVerify      | 32B_k3_m3_Dense_x1 |    440.6 μs |    11.87 μs |     8.58 μs | 2,269.77 |         - |
| OriginalReconstruct  | 32B_k3_m3_Dense_x1 |  2,487.0 μs |   108.16 μs |    84.44 μs |   402.08 |         - |
| OptimizedReconstruct | 32B_k3_m3_Dense_x1 |    995.1 μs |    20.57 μs |    16.06 μs | 1,004.96 |         - |
| **OriginalVerify**       | **32B_k3_m3_Sparse**   |    **825.6 μs** |    **16.96 μs** |    **13.24 μs** | **1,211.25** |         **-** |
| OptimizedVerify      | 32B_k3_m3_Sparse   |    812.0 μs |    13.70 μs |    10.70 μs | 1,231.56 |         - |
| OriginalReconstruct  | 32B_k3_m3_Sparse   |  2,511.2 μs |    68.74 μs |    53.67 μs |   398.21 |         - |
| OptimizedReconstruct | 32B_k3_m3_Sparse   |  1,018.8 μs |    35.07 μs |    27.38 μs |   981.54 |         - |
| **OriginalVerify**       | **32B_k3_m5_Dense**    |    **865.7 μs** |    **44.41 μs** |    **34.67 μs** | **1,155.10** |         **-** |
| OptimizedVerify      | 32B_k3_m5_Dense    |    707.0 μs |    24.45 μs |    16.17 μs | 1,414.38 |         - |
| OriginalReconstruct  | 32B_k3_m5_Dense    |  4,114.8 μs |   128.94 μs |    93.23 μs |   243.03 |         - |
| OptimizedReconstruct | 32B_k3_m5_Dense    |  1,622.4 μs |    37.32 μs |    26.98 μs |   616.36 |         - |
| **OriginalVerify**       | **32B_k3_m9_Sparse**   |    **858.4 μs** |    **31.46 μs** |    **24.56 μs** | **1,164.94** |         **-** |
| OptimizedVerify      | 32B_k3_m9_Sparse   |    816.4 μs |    22.99 μs |    16.62 μs | 1,224.83 |         - |
| OriginalReconstruct  | 32B_k3_m9_Sparse   |  7,321.5 μs |   213.06 μs |   154.06 μs |   136.58 |         - |
| OptimizedReconstruct | 32B_k3_m9_Sparse   |  2,919.1 μs |    96.99 μs |    70.13 μs |   342.57 |         - |
| **OriginalVerify**       | **32B_k5_m5_Dense**    |  **1,375.8 μs** |    **77.58 μs** |    **60.57 μs** |   **726.87** |         **-** |
| OptimizedVerify      | 32B_k5_m5_Dense    |  1,208.3 μs |    64.54 μs |    46.66 μs |   827.63 |         - |
| OriginalReconstruct  | 32B_k5_m5_Dense    |  6,640.3 μs |   357.13 μs |   278.82 μs |   150.59 |         - |
| OptimizedReconstruct | 32B_k5_m5_Dense    |  1,697.8 μs |   106.38 μs |    83.05 μs |   589.00 |         - |
| **OriginalVerify**       | **32B_k5_m5_Sparse**   |  **1,356.1 μs** |    **89.05 μs** |    **69.53 μs** |   **737.38** |         **-** |
| OptimizedVerify      | 32B_k5_m5_Sparse   |  1,307.4 μs |    15.64 μs |    10.34 μs |   764.88 |         - |
| OriginalReconstruct  | 32B_k5_m5_Sparse   |  6,427.5 μs |   302.40 μs |   218.66 μs |   155.58 |         - |
| OptimizedReconstruct | 32B_k5_m5_Sparse   |  1,632.7 μs |    34.93 μs |    23.11 μs |   612.47 |         - |
| **OriginalVerify**       | **32B_k9_m9_Dense**    |  **2,347.5 μs** |    **77.08 μs** |    **55.73 μs** |   **425.99** |         **-** |
| OptimizedVerify      | 32B_k9_m9_Dense    |  2,328.9 μs |    89.30 μs |    69.72 μs |   429.39 |         - |
| OriginalReconstruct  | 32B_k9_m9_Dense    | 20,317.5 μs | 1,084.42 μs |   846.64 μs |    49.22 |         - |
| OptimizedReconstruct | 32B_k9_m9_Dense    |  2,982.4 μs |    40.47 μs |    29.26 μs |   335.30 |         - |
| **OriginalVerify**       | **32B_k9_m9_Dense_x1** |  **2,413.6 μs** |   **152.50 μs** |   **119.06 μs** |   **414.33** |         **-** |
| OptimizedVerify      | 32B_k9_m9_Dense_x1 |    804.0 μs |    24.85 μs |    19.40 μs | 1,243.78 |         - |
| OriginalReconstruct  | 32B_k9_m9_Dense_x1 | 20,688.1 μs | 1,274.98 μs |   995.42 μs |    48.34 |         - |
| OptimizedReconstruct | 32B_k9_m9_Dense_x1 |  3,365.0 μs |   181.89 μs |   142.01 μs |   297.17 |         - |
| **OriginalVerify**       | **32B_k9_m9_Sparse**   |  **2,480.8 μs** |   **157.54 μs** |   **123.00 μs** |   **403.10** |         **-** |
| OptimizedVerify      | 32B_k9_m9_Sparse   |  2,361.1 μs |    93.28 μs |    72.83 μs |   423.54 |         - |
| OriginalReconstruct  | 32B_k9_m9_Sparse   | 21,020.3 μs | 1,481.12 μs | 1,156.36 μs |    47.57 |         - |
| OptimizedReconstruct | 32B_k9_m9_Sparse   |  2,975.6 μs |    90.86 μs |    65.70 μs |   336.07 |         - |
| **OriginalVerify**       | **62B_k3_m3_Dense**    |    **833.5 μs** |    **25.49 μs** |    **18.43 μs** | **1,199.71** |         **-** |
| OptimizedVerify      | 62B_k3_m3_Dense    |    647.2 μs |    28.66 μs |    22.38 μs | 1,545.10 |         - |
| OriginalReconstruct  | 62B_k3_m3_Dense    |  2,596.8 μs |   118.66 μs |    92.64 μs |   385.09 |         - |
| OptimizedReconstruct | 62B_k3_m3_Dense    |  1,015.4 μs |    52.08 μs |    40.66 μs |   984.81 |         - |
| **OriginalVerify**       | **63B_k3_m3_Dense**    |  **1,256.3 μs** |    **59.84 μs** |    **46.72 μs** |   **795.99** |         **-** |
| OptimizedVerify      | 63B_k3_m3_Dense    |    926.9 μs |    21.80 μs |    15.76 μs | 1,078.90 |         - |
| OriginalReconstruct  | 63B_k3_m3_Dense    |  3,640.5 μs |   112.62 μs |    87.93 μs |   274.69 |         - |
| OptimizedReconstruct | 63B_k3_m3_Dense    |  2,419.6 μs |    92.39 μs |    72.13 μs |   413.30 |         - |
| **OriginalVerify**       | **8B_k2_m2_Dense**     |    **313.6 μs** |    **20.97 μs** |    **16.37 μs** | **3,188.39** |         **-** |
| OptimizedVerify      | 8B_k2_m2_Dense     |    207.9 μs |     2.95 μs |     1.95 μs | 4,809.35 |         - |
| OriginalReconstruct  | 8B_k2_m2_Dense     |    647.1 μs |    31.42 μs |    24.53 μs | 1,545.43 |         - |
| OptimizedReconstruct | 8B_k2_m2_Dense     |    364.2 μs |    20.32 μs |    15.87 μs | 2,745.63 |         - |
| **OriginalVerify**       | **8B_k2_m2_Sparse**    |    **317.8 μs** |    **15.73 μs** |    **12.28 μs** | **3,146.23** |         **-** |
| OptimizedVerify      | 8B_k2_m2_Sparse    |    279.0 μs |     4.40 μs |     3.18 μs | 3,584.47 |         - |
| OriginalReconstruct  | 8B_k2_m2_Sparse    |    630.6 μs |     5.14 μs |     3.40 μs | 1,585.90 |         - |
| OptimizedReconstruct | 8B_k2_m2_Sparse    |    355.0 μs |    22.20 μs |    16.05 μs | 2,817.28 |         - |
| **OriginalVerify**       | **8B_k3_m3_Dense**     |    **431.7 μs** |    **11.44 μs** |     **8.27 μs** | **2,316.49** |         **-** |
| OptimizedVerify      | 8B_k3_m3_Dense     |    371.0 μs |    39.54 μs |    28.59 μs | 2,695.24 |         - |
| OriginalReconstruct  | 8B_k3_m3_Dense     |  1,349.0 μs |    93.32 μs |    67.48 μs |   741.26 |         - |
| OptimizedReconstruct | 8B_k3_m3_Dense     |    525.8 μs |    18.23 μs |    13.18 μs | 1,901.89 |         - |
| **OriginalVerify**       | **8B_k3_m3_Sparse**    |    **458.5 μs** |    **25.25 μs** |    **19.72 μs** | **2,181.26** |         **-** |
| OptimizedVerify      | 8B_k3_m3_Sparse    |    414.2 μs |    21.09 μs |    15.25 μs | 2,414.28 |         - |
| OriginalReconstruct  | 8B_k3_m3_Sparse    |  1,384.9 μs |   226.15 μs |   149.59 μs |   722.09 |         - |
| OptimizedReconstruct | 8B_k3_m3_Sparse    |    548.5 μs |    40.03 μs |    31.25 μs | 1,823.23 |         - |
| **OriginalVerify**       | **8B_k5_m5_Dense**     |    **691.0 μs** |    **29.85 μs** |    **23.30 μs** | **1,447.28** |         **-** |
| OptimizedVerify      | 8B_k5_m5_Dense     |    653.0 μs |    89.50 μs |    69.88 μs | 1,531.29 |         - |
| OriginalReconstruct  | 8B_k5_m5_Dense     |  3,360.2 μs |   149.54 μs |   116.75 μs |   297.60 |         - |
| OptimizedReconstruct | 8B_k5_m5_Dense     |    843.6 μs |    15.33 μs |    10.14 μs | 1,185.34 |         - |
| **OriginalVerify**       | **8B_k5_m5_Sparse**    |    **700.2 μs** |    **40.83 μs** |    **31.88 μs** | **1,428.12** |         **-** |
| OptimizedVerify      | 8B_k5_m5_Sparse    |    649.5 μs |    11.03 μs |     7.30 μs | 1,539.53 |         - |
| OriginalReconstruct  | 8B_k5_m5_Sparse    |  3,305.9 μs |    92.97 μs |    61.49 μs |   302.49 |         - |
| OptimizedReconstruct | 8B_k5_m5_Sparse    |    882.4 μs |    53.14 μs |    41.49 μs | 1,133.23 |         - |
| **OriginalVerify**       | **8B_k9_m9_Dense**     |  **1,191.7 μs** |    **75.90 μs** |    **59.25 μs** |   **839.15** |         **-** |
| OptimizedVerify      | 8B_k9_m9_Dense     |  1,187.5 μs |    58.94 μs |    46.02 μs |   842.11 |         - |
| OriginalReconstruct  | 8B_k9_m9_Dense     | 10,447.7 μs |   603.54 μs |   436.40 μs |    95.71 |         - |
| OptimizedReconstruct | 8B_k9_m9_Dense     |  1,537.5 μs |    61.15 μs |    44.22 μs |   650.42 |         - |
| **OriginalVerify**       | **8B_k9_m9_Sparse**    |  **1,211.7 μs** |    **59.24 μs** |    **46.25 μs** |   **825.28** |         **-** |
| OptimizedVerify      | 8B_k9_m9_Sparse    |  1,302.7 μs |   112.27 μs |    87.65 μs |   767.64 |         - |
| OriginalReconstruct  | 8B_k9_m9_Sparse    | 10,177.2 μs |   338.86 μs |   264.56 μs |    98.26 |         - |
| OptimizedReconstruct | 8B_k9_m9_Sparse    |  1,687.0 μs |    97.75 μs |    76.31 μs |   592.77 |         - |
