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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 16.15 ms | 0.645 ms | 0.427 ms | 16.04 ms | 16.80 ms |  1.00 |    0.04 | 62.5000 | 644.88 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 14.15 ms | 1.491 ms | 0.987 ms | 14.02 ms | 15.44 ms |  0.88 |    0.06 | 62.5000 | 618.11 KB |        0.96 |
