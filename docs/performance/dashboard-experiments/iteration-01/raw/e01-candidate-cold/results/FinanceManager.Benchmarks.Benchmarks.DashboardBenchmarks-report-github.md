```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|---------:|---------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 88.88 ms | 6.918 ms | 4.576 ms | 90.28 ms | 93.51 ms |  1.00 |    0.07 | 750.0000 | 250.0000 |   4.98 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 56.22 ms | 3.245 ms | 1.931 ms | 55.93 ms | 59.17 ms |  0.63 |    0.04 | 250.0000 |        - |   2.82 MB |        0.57 |
