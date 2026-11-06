```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 44.3 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-BSNVEC : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  Arguments=/p:UseLocalKeyManager=true,/p:ShouldUnsetParentConfigurationAndPlatform=false  IterationCount=12  
IterationTime=150ms  LaunchCount=1  UnrollFactor=1  
WarmupCount=15  

```
| Method                        | Scenario           | Mean           | Error         | StdDev        | Op/s        | Gen0   | Allocated |
|------------------------------ |------------------- |---------------:|--------------:|--------------:|------------:|-------:|----------:|
| **DirectReplayShareAndSign**      | **Block_AES128_3of5**  |   **536,090.6 ns** |  **17,169.90 ns** |  **13,405.13 ns** |     **1,865.4** |      **-** |         **-** |
| ReplayShareAndSign            | Block_AES128_3of5  |   525,394.1 ns |   4,088.88 ns |   2,704.54 ns |     1,903.3 |      - |         - |
| RecoverKey                    | Block_AES128_3of5  |   601,724.0 ns |   6,796.05 ns |   4,913.99 ns |     1,661.9 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Block_AES128_3of5  |       581.8 ns |       4.01 ns |       3.13 ns | 1,718,692.2 |      - |         - |
| InitializeRecoveredKeyContext | Block_AES128_3of5  |       544.3 ns |       4.56 ns |       3.02 ns | 1,837,084.7 | 0.0109 |      80 B |
| RecoverInitializeAndDecrypt   | Block_AES128_3of5  |   625,381.7 ns |  23,866.98 ns |  18,633.77 ns |     1,599.0 |      - |     113 B |
| **DirectReplayShareAndSign**      | **Block_AES192_3of5**  |   **522,868.6 ns** |   **5,473.47 ns** |   **3,957.68 ns** |     **1,912.5** |      **-** |         **-** |
| ReplayShareAndSign            | Block_AES192_3of5  |   533,126.3 ns |  13,249.16 ns |  10,344.07 ns |     1,875.7 |      - |         - |
| RecoverKey                    | Block_AES192_3of5  |   606,446.4 ns |  13,260.21 ns |   9,588.00 ns |     1,649.0 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Block_AES192_3of5  |       622.5 ns |       3.69 ns |       2.67 ns | 1,606,425.0 |      - |         - |
| InitializeRecoveredKeyContext | Block_AES192_3of5  |       544.1 ns |      14.71 ns |      11.48 ns | 1,837,837.9 | 0.0107 |      80 B |
| RecoverInitializeAndDecrypt   | Block_AES192_3of5  |   611,129.0 ns |  16,187.23 ns |  11,704.43 ns |     1,636.3 |      - |     113 B |
| **DirectReplayShareAndSign**      | **Block_AES256_2of3**  |   **700,221.9 ns** |  **33,170.34 ns** |  **25,897.22 ns** |     **1,428.1** |      **-** |         **-** |
| ReplayShareAndSign            | Block_AES256_2of3  |   677,852.6 ns |  15,883.14 ns |  11,484.55 ns |     1,475.2 |      - |         - |
| RecoverKey                    | Block_AES256_2of3  |   776,655.1 ns |  23,339.03 ns |  18,221.58 ns |     1,287.6 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Block_AES256_2of3  |       652.2 ns |      18.31 ns |      14.30 ns | 1,533,246.0 |      - |         - |
| InitializeRecoveredKeyContext | Block_AES256_2of3  |       567.5 ns |      17.94 ns |      14.01 ns | 1,762,124.1 | 0.0112 |      80 B |
| RecoverInitializeAndDecrypt   | Block_AES256_2of3  |   774,208.5 ns |  26,143.60 ns |  18,903.53 ns |     1,291.6 |      - |     113 B |
| **DirectReplayShareAndSign**      | **Block_AES256_3of5**  | **1,079,216.7 ns** |  **47,931.22 ns** |  **34,657.41 ns** |       **926.6** |      **-** |         **-** |
| ReplayShareAndSign            | Block_AES256_3of5  | 1,005,643.6 ns |  20,648.02 ns |  16,120.62 ns |       994.4 |      - |         - |
| RecoverKey                    | Block_AES256_3of5  | 1,080,956.6 ns |  14,282.09 ns |   9,446.72 ns |       925.1 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Block_AES256_3of5  |       639.0 ns |       7.66 ns |       5.98 ns | 1,565,064.6 |      - |         - |
| InitializeRecoveredKeyContext | Block_AES256_3of5  |       562.6 ns |       8.44 ns |       6.59 ns | 1,777,578.9 | 0.0111 |      80 B |
| RecoverInitializeAndDecrypt   | Block_AES256_3of5  | 1,080,104.1 ns |  16,537.15 ns |   9,840.99 ns |       925.8 |      - |     114 B |
| **DirectReplayShareAndSign**      | **Block_AES256_9of9**  | **2,912,431.3 ns** | **158,346.43 ns** | **123,626.48 ns** |       **343.4** |      **-** |         **-** |
| ReplayShareAndSign            | Block_AES256_9of9  | 2,819,750.6 ns |  41,224.59 ns |  27,267.52 ns |       354.6 |      - |         - |
| RecoverKey                    | Block_AES256_9of9  | 3,120,873.6 ns | 136,929.15 ns | 106,905.28 ns |       320.4 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Block_AES256_9of9  |       663.2 ns |      29.17 ns |      22.78 ns | 1,507,770.1 |      - |         - |
| InitializeRecoveredKeyContext | Block_AES256_9of9  |       595.8 ns |      25.88 ns |      20.21 ns | 1,678,395.4 | 0.0110 |      80 B |
| RecoverInitializeAndDecrypt   | Block_AES256_9of9  | 3,270,572.3 ns | 161,837.21 ns | 107,045.30 ns |       305.8 |      - |     117 B |
| **DirectReplayShareAndSign**      | **Matrix_AES128_3of5** |   **525,762.4 ns** |   **3,364.70 ns** |   **2,432.90 ns** |     **1,902.0** |      **-** |         **-** |
| ReplayShareAndSign            | Matrix_AES128_3of5 |   580,200.7 ns |  29,974.27 ns |  23,401.94 ns |     1,723.5 |      - |         - |
| RecoverKey                    | Matrix_AES128_3of5 |   604,032.4 ns |   8,004.09 ns |   5,294.21 ns |     1,655.5 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Matrix_AES128_3of5 |       608.7 ns |      25.40 ns |      19.83 ns | 1,642,793.8 |      - |         - |
| InitializeRecoveredKeyContext | Matrix_AES128_3of5 |       558.6 ns |      13.24 ns |      10.34 ns | 1,790,119.5 | 0.0109 |      80 B |
| RecoverInitializeAndDecrypt   | Matrix_AES128_3of5 |   613,557.8 ns |  26,871.21 ns |  17,773.64 ns |     1,629.8 |      - |     113 B |
| **DirectReplayShareAndSign**      | **Matrix_AES192_3of5** |   **530,227.6 ns** |  **15,282.40 ns** |  **11,050.17 ns** |     **1,886.0** |      **-** |         **-** |
| ReplayShareAndSign            | Matrix_AES192_3of5 |   563,134.1 ns |  22,466.77 ns |  17,540.58 ns |     1,775.8 |      - |         - |
| RecoverKey                    | Matrix_AES192_3of5 |   640,549.6 ns |  28,356.98 ns |  20,503.96 ns |     1,561.2 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Matrix_AES192_3of5 |       622.1 ns |       7.02 ns |       5.08 ns | 1,607,365.1 |      - |         - |
| InitializeRecoveredKeyContext | Matrix_AES192_3of5 |       551.5 ns |      16.36 ns |      12.77 ns | 1,813,239.3 | 0.0107 |      80 B |
| RecoverInitializeAndDecrypt   | Matrix_AES192_3of5 |   624,075.5 ns |  20,926.76 ns |  16,338.24 ns |     1,602.4 |      - |     113 B |
| **DirectReplayShareAndSign**      | **Matrix_AES256_3of5** | **1,071,919.6 ns** |  **70,454.63 ns** |  **55,006.34 ns** |       **932.9** |      **-** |         **-** |
| ReplayShareAndSign            | Matrix_AES256_3of5 | 1,015,693.4 ns |  41,081.93 ns |  29,704.92 ns |       984.5 |      - |         - |
| RecoverKey                    | Matrix_AES256_3of5 | 1,077,459.1 ns |  14,214.54 ns |   9,402.04 ns |       928.1 |      - |         - |
| ReusedRecoveredKeyDecrypt     | Matrix_AES256_3of5 |       657.8 ns |      22.83 ns |      17.83 ns | 1,520,300.2 |      - |         - |
| InitializeRecoveredKeyContext | Matrix_AES256_3of5 |       566.0 ns |      17.08 ns |      13.33 ns | 1,766,874.4 | 0.0110 |      80 B |
| RecoverInitializeAndDecrypt   | Matrix_AES256_3of5 | 1,149,620.7 ns |  91,812.69 ns |  71,681.31 ns |       869.9 |      - |     114 B |
