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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 15.69 ms | 0.407 ms | 0.269 ms | 15.74 ms | 16.02 ms |  1.00 |    0.02 | 66.6667 | 645.83 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 15.50 ms | 1.380 ms | 0.913 ms | 15.55 ms | 16.79 ms |  0.99 |    0.06 | 62.5000 | 613.65 KB |        0.95 |
