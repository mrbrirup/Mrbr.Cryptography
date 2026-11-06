```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 43.38 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-SGCRMK : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  IterationCount=15  IterationTime=300ms  
LaunchCount=2  UnrollFactor=1  WarmupCount=15  

```
| Method                   | Scenario          | Mean      | Error     | StdDev    | Op/s   | Allocated |
|------------------------- |------------------ |----------:|----------:|----------:|-------:|----------:|
| **PlainReconstruct**         | **1024B_k3_m3_Dense** | **25.968 ms** | **0.6529 ms** | **0.9773 ms** |  **38.51** |         **-** |
| AuthenticatedReconstruct | 1024B_k3_m3_Dense | 25.753 ms | 0.5612 ms | 0.8048 ms |  38.83 |         - |
| **PlainReconstruct**         | **32B_k9_m9_Dense**   |  **3.053 ms** | **0.0893 ms** | **0.1337 ms** | **327.57** |         **-** |
| AuthenticatedReconstruct | 32B_k9_m9_Dense   |  3.032 ms | 0.0124 ms | 0.0166 ms | 329.80 |         - |
