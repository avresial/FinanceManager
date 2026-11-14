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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 15.54 ms | 0.455 ms | 0.301 ms | 15.40 ms | 15.96 ms |  1.00 |    0.03 | 62.5000 |  645.9 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 13.96 ms | 1.273 ms | 0.842 ms | 13.54 ms | 15.02 ms |  0.90 |    0.05 | 58.8235 | 615.71 KB |        0.95 |
