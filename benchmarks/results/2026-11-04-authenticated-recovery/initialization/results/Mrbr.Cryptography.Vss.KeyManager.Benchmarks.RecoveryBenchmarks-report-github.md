```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 43.94 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-BSNVEC : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  Arguments=/p:UseLocalKeyManager=true,/p:ShouldUnsetParentConfigurationAndPlatform=false  IterationCount=12  
IterationTime=150ms  LaunchCount=1  UnrollFactor=1  
WarmupCount=15  

```
| Method                        | Scenario           | Mean     | Error    | StdDev   | Op/s        | Gen0   | Allocated |
|------------------------------ |------------------- |---------:|---------:|---------:|------------:|-------:|----------:|
| **InitializeRecoveredKeyContext** | **Block_AES128_3of5**  | **566.2 ns** |  **7.08 ns** |  **5.12 ns** | **1,766,194.8** | **0.0153** |     **112 B** |
| **InitializeRecoveredKeyContext** | **Block_AES192_3of5**  | **559.7 ns** | **25.44 ns** | **19.87 ns** | **1,786,817.5** | **0.0146** |     **112 B** |
| **InitializeRecoveredKeyContext** | **Block_AES256_2of3**  | **572.4 ns** | **27.62 ns** | **21.56 ns** | **1,746,919.6** | **0.0154** |     **112 B** |
| **InitializeRecoveredKeyContext** | **Block_AES256_3of5**  | **554.8 ns** |  **9.05 ns** |  **5.99 ns** | **1,802,354.3** | **0.0148** |     **112 B** |
| **InitializeRecoveredKeyContext** | **Block_AES256_9of9**  | **564.8 ns** | **12.84 ns** |  **9.28 ns** | **1,770,482.3** | **0.0148** |     **112 B** |
| **InitializeRecoveredKeyContext** | **Matrix_AES128_3of5** | **561.2 ns** | **11.63 ns** |  **7.69 ns** | **1,781,893.0** | **0.0151** |     **112 B** |
| **InitializeRecoveredKeyContext** | **Matrix_AES192_3of5** | **567.0 ns** | **13.15 ns** | **10.27 ns** | **1,763,684.1** | **0.0149** |     **112 B** |
| **InitializeRecoveredKeyContext** | **Matrix_AES256_3of5** | **558.8 ns** |  **7.59 ns** |  **5.49 ns** | **1,789,578.2** | **0.0148** |     **112 B** |
