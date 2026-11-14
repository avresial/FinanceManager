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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 83.88 ms | 6.404 ms | 4.236 ms | 83.63 ms | 89.21 ms |  1.00 |    0.07 | 750.0000 | 250.0000 |   5.37 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 57.18 ms | 3.260 ms | 2.156 ms | 57.39 ms | 60.07 ms |  0.68 |    0.04 | 250.0000 |        - |   2.89 MB |        0.54 |
