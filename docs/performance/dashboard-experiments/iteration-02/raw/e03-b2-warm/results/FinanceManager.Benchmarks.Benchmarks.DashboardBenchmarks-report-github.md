```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|--------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 16.29 ms | 0.401 ms | 0.265 ms | 16.31 ms | 16.66 ms |  1.00 |    0.02 | 62.5000 | 600.63 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 13.82 ms | 0.740 ms | 0.441 ms | 13.90 ms | 14.39 ms |  0.85 |    0.03 | 71.4286 |  569.5 KB |        0.95 |
