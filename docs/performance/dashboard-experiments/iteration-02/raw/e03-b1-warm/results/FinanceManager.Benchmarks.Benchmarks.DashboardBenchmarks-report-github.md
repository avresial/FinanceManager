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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 16.25 ms | 0.683 ms | 0.407 ms | 16.20 ms | 16.89 ms |  1.00 |    0.03 | 66.6667 | 601.16 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 13.59 ms | 1.081 ms | 0.643 ms | 13.63 ms | 14.32 ms |  0.84 |    0.04 | 62.5000 | 568.67 KB |        0.95 |
