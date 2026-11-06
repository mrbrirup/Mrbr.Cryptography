```

BenchmarkDotNet v0.16.0-preview.2, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-10750H CPU 2.60GHz (Max: 2.59GHz), 1 CPU, 12 logical and 6 physical cores
Memory: 63.83 GB Total, 42.75 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  Job-RCBXQR : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

PowerPlanMode=  IterationCount=12  IterationTime=120ms  
LaunchCount=1  UnrollFactor=1  WarmupCount=15  

```
| Method                   | Scenario          | Mean         | Error      | StdDev     | Op/s      | Allocated |
|------------------------- |------------------ |-------------:|-----------:|-----------:|----------:|----------:|
| **CreateEnvelope**           | **1024B_k3_m3_Dense** |     **87.09 μs** |   **2.157 μs** |   **1.559 μs** | **11,481.92** |         **-** |
| AuthenticatePacket       | 1024B_k3_m3_Dense |    104.24 μs |   4.040 μs |   3.154 μs |  9,593.47 |         - |
| PlainVerifyShare         | 1024B_k3_m3_Dense | 10,340.64 μs | 194.042 μs | 140.305 μs |     96.71 |         - |
| AuthenticatedVerifyShare | 1024B_k3_m3_Dense | 10,443.00 μs | 111.986 μs |  87.432 μs |     95.76 |         - |
| PlainReconstruct         | 1024B_k3_m3_Dense | 25,489.45 μs | 861.024 μs | 622.577 μs |     39.23 |         - |
| AuthenticatedReconstruct | 1024B_k3_m3_Dense | 25,499.15 μs | 585.916 μs | 387.547 μs |     39.22 |         - |
| **CreateEnvelope**           | **32B_k2_m2_Dense**   |     **73.27 μs** |   **3.219 μs** |   **2.513 μs** | **13,648.21** |         **-** |
| AuthenticatePacket       | 32B_k2_m2_Dense   |     96.21 μs |   2.386 μs |   1.863 μs | 10,394.22 |         - |
| PlainVerifyShare         | 32B_k2_m2_Dense   |    422.90 μs |   8.337 μs |   6.509 μs |  2,364.65 |         - |
| AuthenticatedVerifyShare | 32B_k2_m2_Dense   |    517.14 μs |   7.653 μs |   5.062 μs |  1,933.72 |         - |
| PlainReconstruct         | 32B_k2_m2_Dense   |    681.90 μs |  16.302 μs |  12.727 μs |  1,466.49 |         - |
| AuthenticatedReconstruct | 32B_k2_m2_Dense   |    788.27 μs |  49.412 μs |  38.578 μs |  1,268.60 |         - |
| **CreateEnvelope**           | **32B_k3_m3_Dense**   |     **72.33 μs** |   **1.543 μs** |   **1.204 μs** | **13,826.03** |         **-** |
| AuthenticatePacket       | 32B_k3_m3_Dense   |     96.89 μs |   3.235 μs |   2.525 μs | 10,321.20 |         - |
| PlainVerifyShare         | 32B_k3_m3_Dense   |    621.24 μs |  23.099 μs |  18.034 μs |  1,609.68 |         - |
| AuthenticatedVerifyShare | 32B_k3_m3_Dense   |    707.67 μs |  15.269 μs |  11.041 μs |  1,413.09 |         - |
| PlainReconstruct         | 32B_k3_m3_Dense   |  1,023.55 μs |  48.665 μs |  37.995 μs |    976.99 |         - |
| AuthenticatedReconstruct | 32B_k3_m3_Dense   |  1,091.12 μs |  21.289 μs |  14.082 μs |    916.49 |         - |
| **CreateEnvelope**           | **32B_k3_m3_Sparse**  |     **72.45 μs** |   **1.173 μs** |   **0.848 μs** | **13,802.15** |         **-** |
| AuthenticatePacket       | 32B_k3_m3_Sparse  |     99.06 μs |   5.875 μs |   4.587 μs | 10,094.60 |         - |
| PlainVerifyShare         | 32B_k3_m3_Sparse  |    819.69 μs |  29.310 μs |  19.387 μs |  1,219.97 |         - |
| AuthenticatedVerifyShare | 32B_k3_m3_Sparse  |    917.39 μs |  18.366 μs |  13.280 μs |  1,090.05 |         - |
| PlainReconstruct         | 32B_k3_m3_Sparse  |  1,009.69 μs |  18.984 μs |  12.557 μs |    990.40 |         - |
| AuthenticatedReconstruct | 32B_k3_m3_Sparse  |  1,121.79 μs |  60.974 μs |  47.604 μs |    891.43 |         - |
| **CreateEnvelope**           | **32B_k5_m5_Dense**   |     **74.18 μs** |   **2.194 μs** |   **1.713 μs** | **13,480.02** |         **-** |
| AuthenticatePacket       | 32B_k5_m5_Dense   |     94.70 μs |   0.833 μs |   0.602 μs | 10,559.30 |         - |
| PlainVerifyShare         | 32B_k5_m5_Dense   |  1,120.95 μs |  52.169 μs |  40.730 μs |    892.10 |         - |
| AuthenticatedVerifyShare | 32B_k5_m5_Dense   |  1,188.75 μs |  15.435 μs |  11.160 μs |    841.22 |         - |
| PlainReconstruct         | 32B_k5_m5_Dense   |  1,695.16 μs |  85.326 μs |  66.617 μs |    589.92 |         - |
| AuthenticatedReconstruct | 32B_k5_m5_Dense   |  1,792.53 μs |  88.902 μs |  69.409 μs |    557.87 |         - |
| **CreateEnvelope**           | **32B_k9_m9_Dense**   |     **75.11 μs** |   **3.871 μs** |   **3.022 μs** | **13,313.67** |         **-** |
| AuthenticatePacket       | 32B_k9_m9_Dense   |     99.80 μs |   5.924 μs |   4.625 μs | 10,020.45 |         - |
| PlainVerifyShare         | 32B_k9_m9_Dense   |  2,256.76 μs |  23.451 μs |  15.511 μs |    443.11 |         - |
| AuthenticatedVerifyShare | 32B_k9_m9_Dense   |  2,383.87 μs |  39.081 μs |  25.850 μs |    419.49 |         - |
| PlainReconstruct         | 32B_k9_m9_Dense   |  3,252.47 μs | 182.356 μs | 142.372 μs |    307.46 |         - |
| AuthenticatedReconstruct | 32B_k9_m9_Dense   |  3,121.25 μs | 127.132 μs |  99.256 μs |    320.38 |         - |
| **CreateEnvelope**           | **62B_k3_m3_Dense**   |     **75.23 μs** |   **5.229 μs** |   **4.082 μs** | **13,291.92** |         **-** |
| AuthenticatePacket       | 62B_k3_m3_Dense   |     97.89 μs |   5.227 μs |   4.081 μs | 10,215.46 |         - |
| PlainVerifyShare         | 62B_k3_m3_Dense   |    639.71 μs |  29.195 μs |  22.793 μs |  1,563.22 |         - |
| AuthenticatedVerifyShare | 62B_k3_m3_Dense   |    767.73 μs |  37.903 μs |  27.406 μs |  1,302.53 |         - |
| PlainReconstruct         | 62B_k3_m3_Dense   |  1,004.78 μs |  27.948 μs |  21.820 μs |    995.24 |         - |
| AuthenticatedReconstruct | 62B_k3_m3_Dense   |  1,141.38 μs |  90.929 μs |  70.991 μs |    876.13 |         - |
| **CreateEnvelope**           | **8B_k3_m3_Dense**    |     **74.03 μs** |   **4.612 μs** |   **3.601 μs** | **13,508.82** |         **-** |
| AuthenticatePacket       | 8B_k3_m3_Dense    |    101.50 μs |   3.967 μs |   2.624 μs |  9,851.99 |         - |
| PlainVerifyShare         | 8B_k3_m3_Dense    |    317.22 μs |  12.815 μs |  10.005 μs |  3,152.40 |         - |
| AuthenticatedVerifyShare | 8B_k3_m3_Dense    |    407.24 μs |  12.244 μs |   9.559 μs |  2,455.54 |         - |
| PlainReconstruct         | 8B_k3_m3_Dense    |    516.57 μs |  12.799 μs |   9.255 μs |  1,935.86 |         - |
| AuthenticatedReconstruct | 8B_k3_m3_Dense    |    633.22 μs |  41.027 μs |  29.665 μs |  1,579.22 |         - |
