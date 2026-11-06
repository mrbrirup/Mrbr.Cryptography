```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 43.06 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-TUKPWG : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  IterationCount=15  IterationTime=150ms  
LaunchCount=1  UnrollFactor=1  WarmupCount=20  

```
| Method                 | Scenario   | Mean             | Error          | StdDev         | Op/s          | Allocated |
|----------------------- |----------- |-----------------:|---------------:|---------------:|--------------:|----------:|
| **ShamirCreate**           | **1024B_3of5** |      **4,561.26 ns** |     **100.765 ns** |      **94.255 ns** |    **219,237.56** |         **-** |
| VssCreate              | 1024B_3of5 | 15,569,118.00 ns | 317,284.750 ns | 296,788.352 ns |         64.23 |         - |
| VssVerifyOne           | 1024B_3of5 | 14,176,884.85 ns | 662,673.261 ns | 619,864.980 ns |         70.54 |         - |
| ShamirReconstruct      | 1024B_3of5 |        223.52 ns |       6.049 ns |       5.658 ns |  4,473,828.24 |         - |
| VssVerifiedReconstruct | 1024B_3of5 | 39,861,245.00 ns | 620,329.881 ns | 580,256.956 ns |         25.09 |         - |
| **ShamirCreate**           | **32B_2of3**   |        **249.94 ns** |       **9.282 ns** |       **8.683 ns** |  **4,001,036.85** |         **-** |
| VssCreate              | 32B_2of3   |    632,813.96 ns |  11,023.848 ns |  10,311.714 ns |      1,580.24 |         - |
| VssVerifyOne           | 32B_2of3   |    584,567.88 ns |   8,553.116 ns |   7,142.239 ns |      1,710.67 |         - |
| ShamirReconstruct      | 32B_2of3   |         42.72 ns |       1.844 ns |       1.725 ns | 23,407,295.21 |         - |
| VssVerifiedReconstruct | 32B_2of3   |  1,192,181.63 ns |  27,700.324 ns |  25,910.900 ns |        838.80 |         - |
| **ShamirCreate**           | **32B_3of5**   |        **600.15 ns** |      **14.988 ns** |      **14.020 ns** |  **1,666,253.17** |         **-** |
| VssCreate              | 32B_3of5   |    936,467.02 ns |  13,916.008 ns |  10,864.703 ns |      1,067.84 |         - |
| VssVerifyOne           | 32B_3of5   |    822,209.44 ns |   3,640.403 ns |   2,842.187 ns |      1,216.24 |         - |
| ShamirReconstruct      | 32B_3of5   |         57.31 ns |       1.591 ns |       1.488 ns | 17,448,885.50 |         - |
| VssVerifiedReconstruct | 32B_3of5   |  2,486,244.50 ns |  34,880.125 ns |  30,920.322 ns |        402.21 |         - |
| **ShamirCreate**           | **32B_5of7**   |        **866.31 ns** |      **26.678 ns** |      **23.650 ns** |  **1,154,322.40** |         **-** |
| VssCreate              | 32B_5of7   |  1,563,225.34 ns |  28,866.366 ns |  24,104.723 ns |        639.70 |         - |
| VssVerifyOne           | 32B_5of7   |  1,351,129.26 ns |  50,186.202 ns |  44,488.761 ns |        740.12 |         - |
| ShamirReconstruct      | 32B_5of7   |         99.13 ns |       5.987 ns |       5.600 ns | 10,087,283.96 |         - |
| VssVerifiedReconstruct | 32B_5of7   |  6,371,846.96 ns | 106,695.758 ns |  99,803.279 ns |        156.94 |         - |
| **ShamirCreate**           | **32B_9of9**   |      **1,108.01 ns** |      **42.381 ns** |      **39.644 ns** |    **902,516.47** |         **-** |
| VssCreate              | 32B_9of9   |  2,871,038.15 ns | 153,303.334 ns | 143,400.034 ns |        348.31 |         - |
| VssVerifyOne           | 32B_9of9   |  2,350,456.21 ns | 101,126.596 ns |  94,593.881 ns |        425.45 |         - |
| ShamirReconstruct      | 32B_9of9   |        186.05 ns |      10.223 ns |       9.563 ns |  5,374,895.18 |         - |
| VssVerifiedReconstruct | 32B_9of9   | 19,587,830.36 ns | 210,641.237 ns | 186,727.970 ns |         51.05 |         - |
| **ShamirCreate**           | **8B_2of3**    |        **436.96 ns** |      **17.877 ns** |      **16.722 ns** |  **2,288,531.13** |         **-** |
| VssCreate              | 8B_2of3    |    339,693.62 ns |  13,026.201 ns |  12,184.716 ns |      2,943.83 |         - |
| VssVerifyOne           | 8B_2of3    |    307,087.76 ns |   7,636.240 ns |   7,142.944 ns |      3,256.40 |         - |
| ShamirReconstruct      | 8B_2of3    |        168.84 ns |       6.765 ns |       6.328 ns |  5,922,783.62 |         - |
| VssVerifiedReconstruct | 8B_2of3    |    634,471.77 ns |  20,066.393 ns |  16,756.347 ns |      1,576.11 |         - |
| **ShamirCreate**           | **8B_3of5**    |      **1,052.46 ns** |      **45.219 ns** |      **42.298 ns** |    **950,157.26** |         **-** |
| VssCreate              | 8B_3of5    |    505,381.51 ns |  34,657.610 ns |  32,418.750 ns |      1,978.70 |         - |
| VssVerifyOne           | 8B_3of5    |    424,214.68 ns |  11,267.287 ns |   9,408.695 ns |      2,357.30 |         - |
| ShamirReconstruct      | 8B_3of5    |        247.86 ns |       2.766 ns |       2.309 ns |  4,034,487.69 |         - |
| VssVerifiedReconstruct | 8B_3of5    |  1,336,972.52 ns |  76,867.481 ns |  71,901.889 ns |        747.96 |         - |
| **ShamirCreate**           | **8B_5of7**    |      **2,358.12 ns** |      **39.966 ns** |      **35.428 ns** |    **424,066.29** |         **-** |
| VssCreate              | 8B_5of7    |    811,260.74 ns |  29,760.347 ns |  27,837.847 ns |      1,232.65 |         - |
| VssVerifyOne           | 8B_5of7    |    688,926.04 ns |  16,521.910 ns |  14,646.242 ns |      1,451.53 |         - |
| ShamirReconstruct      | 8B_5of7    |        435.29 ns |      18.124 ns |      16.066 ns |  2,297,339.88 |         - |
| VssVerifiedReconstruct | 8B_5of7    |  3,421,263.62 ns | 162,759.505 ns | 152,245.342 ns |        292.29 |         - |
