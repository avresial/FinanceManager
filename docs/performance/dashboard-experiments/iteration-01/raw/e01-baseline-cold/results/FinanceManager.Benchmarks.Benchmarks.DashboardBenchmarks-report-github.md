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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 88.79 ms | 6.403 ms | 4.235 ms | 89.54 ms | 93.77 ms |  1.00 |    0.07 | 750.0000 | 250.0000 |   5.31 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 55.74 ms | 2.526 ms | 1.671 ms | 55.66 ms | 57.94 ms |  0.63 |    0.03 | 250.0000 |        - |   2.89 MB |        0.54 |
