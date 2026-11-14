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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 16.35 ms | 0.911 ms | 0.603 ms | 16.30 ms | 17.15 ms |  1.00 |    0.05 | 66.6667 | 644.85 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 15.03 ms | 1.876 ms | 1.241 ms | 15.14 ms | 16.74 ms |  0.92 |    0.08 | 66.6667 | 615.39 KB |        0.95 |
