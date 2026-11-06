```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 43.33 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-BKQOOB : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  Arguments=/p:ComparisonTargetFramework=net11.0  IterationCount=12  
IterationTime=150ms  LaunchCount=1  UnrollFactor=1  
WarmupCount=15  

```
| Method                    | Scenario  | Mean              | Error             | StdDev            | Op/s           | Gen0      | Gen1    | Allocated  |
|-------------------------- |---------- |------------------:|------------------:|------------------:|---------------:|----------:|--------:|-----------:|
| **MrbrCreate**                | **128B_3of5** |       **1,120.56 ns** |         **16.709 ns** |         **12.081 ns** |    **892,410.649** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 128B_3of5 |          73.13 ns |          0.528 ns |          0.382 ns | 13,673,859.990 |         - |       - |          - |
| UpstreamBigCreate         | 128B_3of5 |      24,835.40 ns |        482.791 ns |        349.089 ns |     40,265.109 |    4.7471 |  1.8006 |    29820 B |
| UpstreamSecureCreate      | 128B_3of5 |   2,340,068.38 ns |     25,766.814 ns |     15,333.418 ns |        427.338 |         - |       - |    21320 B |
| UpstreamBigReconstruct    | 128B_3of5 |      72,571.80 ns |      1,860.300 ns |      1,345.119 ns |     13,779.456 |   10.2489 |  4.8804 |    65336 B |
| UpstreamSecureReconstruct | 128B_3of5 | 743,409,279.17 ns | 43,455,523.553 ns | 33,927,214.415 ns |          1.345 | 3000.0000 |       - | 19694400 B |
| **MrbrCreate**                | **16B_3of5**  |         **489.22 ns** |          **4.212 ns** |          **3.046 ns** |  **2,044,085.861** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 16B_3of5  |          62.46 ns |          3.412 ns |          2.664 ns | 16,009,649.419 |         - |       - |          - |
| UpstreamBigCreate         | 16B_3of5  |      19,307.57 ns |        800.432 ns |        578.765 ns |     51,793.148 |    3.5434 |  3.4169 |    22958 B |
| UpstreamSecureCreate      | 16B_3of5  |     756,150.28 ns |     46,739.465 ns |     36,491.100 ns |      1,322.488 |         - |       - |    21192 B |
| UpstreamBigReconstruct    | 16B_3of5  |      48,997.96 ns |      1,335.417 ns |        883.296 ns |     20,409.012 |    8.5358 |  8.2075 |    54912 B |
| UpstreamSecureReconstruct | 16B_3of5  | 196,300,441.67 ns | 11,172,007.103 ns |  8,722,368.285 ns |          5.094 | 1250.0000 |       - |  8076064 B |
| **MrbrCreate**                | **24B_3of5**  |       **1,362.34 ns** |         **13.877 ns** |         **10.835 ns** |    **734,032.295** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 24B_3of5  |         294.55 ns |          4.632 ns |          3.350 ns |  3,395,056.317 |         - |       - |          - |
| UpstreamBigCreate         | 24B_3of5  |      18,719.94 ns |        317.033 ns |        247.519 ns |     53,418.972 |    3.5653 |  3.4423 |    22966 B |
| UpstreamSecureCreate      | 24B_3of5  |     755,954.94 ns |     74,788.640 ns |     58,390.051 ns |      1,322.830 |         - |       - |    21192 B |
| UpstreamBigReconstruct    | 24B_3of5  |      49,753.47 ns |      1,176.998 ns |        918.922 ns |     20,099.101 |    8.5667 |  8.2372 |    54936 B |
| UpstreamSecureReconstruct | 24B_3of5  | 186,432,552.08 ns | 16,321,991.321 ns | 12,743,137.212 ns |          5.364 | 1250.0000 |       - |  8076064 B |
| **MrbrCreate**                | **32B_2of3**  |         **247.55 ns** |          **3.004 ns** |          **2.345 ns** |  **4,039,617.928** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 32B_2of3  |          46.98 ns |          2.355 ns |          1.703 ns | 21,284,189.182 |         - |       - |          - |
| UpstreamBigCreate         | 32B_2of3  |      11,593.35 ns |        482.809 ns |        349.103 ns |     86,256.309 |    1.8422 |  1.7654 |    11591 B |
| UpstreamSecureCreate      | 32B_2of3  |     466,567.66 ns |     44,917.700 ns |     35,068.785 ns |      2,143.312 |         - |       - |    11560 B |
| UpstreamBigReconstruct    | 32B_2of3  |      34,505.05 ns |        382.196 ns |        298.393 ns |     28,981.266 |    5.6617 |  5.4258 |    35912 B |
| UpstreamSecureReconstruct | 32B_2of3  | 129,354,447.50 ns |    845,040.766 ns |    558,942.145 ns |          7.731 |  750.0000 |       - |  5921264 B |
| **MrbrCreate**                | **32B_3of5**  |         **599.05 ns** |          **8.727 ns** |          **6.310 ns** |  **1,669,311.191** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 32B_3of5  |          60.12 ns |          0.421 ns |          0.250 ns | 16,633,534.353 |         - |       - |          - |
| UpstreamBigCreate         | 32B_3of5  |      19,562.02 ns |        626.626 ns |        489.228 ns |     51,119.475 |    3.5662 |  3.4342 |    22974 B |
| UpstreamSecureCreate      | 32B_3of5  |     756,660.33 ns |     50,905.695 ns |     39,743.818 ns |      1,321.597 |         - |       - |    21192 B |
| UpstreamBigReconstruct    | 32B_3of5  |      49,998.89 ns |      1,182.782 ns |        855.229 ns |     20,000.444 |    8.4967 |  8.1699 |    54960 B |
| UpstreamSecureReconstruct | 32B_3of5  | 185,019,415.91 ns |    697,102.700 ns |    504,050.926 ns |          5.405 | 1250.0000 |       - |  8076064 B |
| **MrbrCreate**                | **32B_5of7**  |         **825.48 ns** |         **50.506 ns** |         **39.431 ns** |  **1,211,414.413** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 32B_5of7  |         104.02 ns |          6.628 ns |          5.175 ns |  9,613,833.267 |         - |       - |          - |
| UpstreamBigCreate         | 32B_5of7  |      32,733.14 ns |        442.211 ns |        292.496 ns |     30,550.077 |    7.2816 |  0.2207 |    45997 B |
| UpstreamSecureCreate      | 32B_5of7  |   1,324,419.91 ns |      9,300.644 ns |      6,151.800 ns |        755.048 |         - |       - |    40568 B |
| UpstreamBigReconstruct    | 32B_5of7  |      87,672.62 ns |      2,236.816 ns |      1,746.359 ns |     11,406.069 |   16.2297 | 15.6055 |   103144 B |
| UpstreamSecureReconstruct | 32B_5of7  | 307,017,013.89 ns |  3,673,300.357 ns |  2,185,922.123 ns |          3.257 | 1750.0000 |       - | 12259720 B |
| **MrbrCreate**                | **32B_9of9**  |       **1,104.65 ns** |         **61.828 ns** |         **40.896 ns** |    **905,260.308** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 32B_9of9  |         190.92 ns |         15.473 ns |         12.080 ns |  5,237,827.698 |         - |       - |          - |
| UpstreamBigCreate         | 32B_9of9  |      66,650.13 ns |      4,253.072 ns |      3,320.519 ns |     15,003.722 |   15.0691 |  7.1159 |    96334 B |
| UpstreamSecureCreate      | 32B_9of9  |   2,505,236.71 ns |     18,808.425 ns |     13,599.723 ns |        399.164 |         - |       - |    82792 B |
| UpstreamBigReconstruct    | 32B_9of9  |     269,723.15 ns |      6,736.052 ns |      4,455.481 ns |      3,707.505 |   55.3435 | 26.7176 |   351472 B |
| UpstreamSecureReconstruct | 32B_9of9  | 487,429,729.17 ns | 33,448,674.929 ns | 26,114,525.230 ns |          2.052 | 3000.0000 |       - | 20312888 B |
| **MrbrCreate**                | **8B_3of5**   |       **1,007.64 ns** |         **32.077 ns** |         **23.193 ns** |    **992,415.051** |         **-** |       **-** |          **-** |
| MrbrReconstruct           | 8B_3of5   |         261.79 ns |         15.547 ns |         12.138 ns |  3,819,915.707 |         - |       - |          - |
| UpstreamBigCreate         | 8B_3of5   |      15,619.14 ns |        958.177 ns |        692.825 ns |     64,024.027 |    2.9334 |  2.8323 |    18664 B |
| UpstreamSecureCreate      | 8B_3of5   |     369,421.00 ns |     18,802.476 ns |     14,679.736 ns |      2,706.939 |    2.3419 |       - |    20808 B |
| UpstreamBigReconstruct    | 8B_3of5   |      43,698.94 ns |      1,234.881 ns |        964.114 ns |     22,883.852 |    7.5868 |  7.2950 |    48504 B |
| UpstreamSecureReconstruct | 8B_3of5   |  23,081,591.43 ns |    312,315.964 ns |    206,577.673 ns |         43.325 |  142.8571 |       - |  1452896 B |
