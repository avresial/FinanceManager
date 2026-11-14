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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 15.98 ms | 0.546 ms | 0.361 ms | 15.96 ms | 16.38 ms |  1.00 |    0.03 | 62.5000 | 645.16 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 15.08 ms | 1.203 ms | 0.716 ms | 15.38 ms | 15.64 ms |  0.94 |    0.05 | 66.6667 | 618.63 KB |        0.96 |
