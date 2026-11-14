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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 17.99 ms | 3.123 ms | 2.066 ms | 17.30 ms | 21.20 ms |  1.01 |    0.15 | 66.6667 | 644.25 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 14.57 ms | 1.204 ms | 0.796 ms | 14.67 ms | 15.65 ms |  0.82 |    0.10 | 66.6667 | 614.21 KB |        0.95 |
