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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 85.11 ms | 4.334 ms | 2.867 ms | 85.35 ms | 89.36 ms |  1.00 |    0.05 | 750.0000 | 250.0000 |   5.33 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 54.37 ms | 2.004 ms | 1.193 ms | 54.18 ms | 56.06 ms |  0.64 |    0.02 | 250.0000 |        - |   2.89 MB |        0.54 |
