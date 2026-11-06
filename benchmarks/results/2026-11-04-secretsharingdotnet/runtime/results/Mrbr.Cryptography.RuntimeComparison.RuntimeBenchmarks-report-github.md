```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 43.99 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]    : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
  Runtime10 : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
  Runtime11 : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  IterationCount=12  IterationTime=150ms  
LaunchCount=1  UnrollFactor=1  WarmupCount=15  

```
| Method            | Job       | Runtime   | Length | Mean          | Error         | StdDev        | Op/s       | Gen0      | Gen1   | Allocated   |
|------------------ |---------- |---------- |------- |--------------:|--------------:|--------------:|-----------:|----------:|-------:|------------:|
| **BigCreate**         | **Runtime10** | **.NET 10.0** | **32**     |      **16.74 μs** |      **0.566 μs** |      **0.374 μs** | **59,724.195** |    **3.6752** | **3.5639** |    **23.09 KB** |
| SecureCreate      | Runtime10 | .NET 10.0 | 32     |     773.69 μs |     74.469 μs |     58.140 μs |  1,292.511 |         - |      - |     20.7 KB |
| BigReconstruct    | Runtime10 | .NET 10.0 | 32     |      42.91 μs |      3.357 μs |      2.621 μs | 23,306.579 |    8.9781 | 8.7140 |    55.55 KB |
| SecureReconstruct | Runtime10 | .NET 10.0 | 32     | 187,722.42 μs |  1,640.281 μs |  1,084.945 μs |      5.327 | 1250.0000 |      - |  7886.78 KB |
| BigCreate         | Runtime11 | .NET 11.0 | 32     |      18.49 μs |      0.443 μs |      0.320 μs | 54,078.980 |    3.5651 | 3.4462 |    22.44 KB |
| SecureCreate      | Runtime11 | .NET 11.0 | 32     |     737.11 μs |     44.346 μs |     34.622 μs |  1,356.658 |         - |      - |     20.7 KB |
| BigReconstruct    | Runtime11 | .NET 11.0 | 32     |      50.41 μs |      2.628 μs |      2.052 μs | 19,836.661 |    8.4879 | 8.1735 |    53.67 KB |
| SecureReconstruct | Runtime11 | .NET 11.0 | 32     | 182,043.53 μs |    696.740 μs |    543.969 μs |      5.493 | 1250.0000 |      - |  7886.78 KB |
| **BigCreate**         | **Runtime10** | **.NET 10.0** | **128**    |      **21.39 μs** |      **0.358 μs** |      **0.259 μs** | **46,746.322** |    **4.8126** | **2.3334** |    **29.78 KB** |
| SecureCreate      | Runtime10 | .NET 10.0 | 128    |   2,477.65 μs |     33.795 μs |     20.111 μs |    403.608 |         - |      - |    20.82 KB |
| BigReconstruct    | Runtime10 | .NET 10.0 | 128    |      70.00 μs |      4.147 μs |      3.237 μs | 14,284.847 |   10.4302 | 5.2151 |    65.69 KB |
| SecureReconstruct | Runtime10 | .NET 10.0 | 128    | 763,818.08 μs | 53,605.839 μs | 41,851.913 μs |      1.309 | 3000.0000 |      - | 19232.81 KB |
| BigCreate         | Runtime11 | .NET 11.0 | 128    |      24.44 μs |      1.185 μs |      0.925 μs | 40,914.635 |    4.6609 | 1.6072 |    29.12 KB |
| SecureCreate      | Runtime11 | .NET 11.0 | 128    |   2,327.79 μs |     23.492 μs |     18.341 μs |    429.592 |         - |      - |    20.82 KB |
| BigReconstruct    | Runtime11 | .NET 11.0 | 128    |      70.32 μs |      4.021 μs |      3.139 μs | 14,220.592 |   10.3697 | 4.9594 |     63.8 KB |
| SecureReconstruct | Runtime11 | .NET 11.0 | 128    | 817,212.92 μs | 43,860.673 μs | 34,243.529 μs |      1.224 | 3000.0000 |      - | 19232.81 KB |
