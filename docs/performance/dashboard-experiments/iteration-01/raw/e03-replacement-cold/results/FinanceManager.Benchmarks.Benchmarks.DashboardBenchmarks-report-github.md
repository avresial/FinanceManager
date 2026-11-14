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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 84.69 ms | 7.220 ms | 4.775 ms | 85.66 ms | 90.06 ms |  1.00 |    0.08 | 750.0000 | 250.0000 |   5.26 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 56.30 ms | 3.035 ms | 2.008 ms | 55.99 ms | 59.42 ms |  0.67 |    0.04 | 250.0000 |        - |   2.84 MB |        0.54 |
