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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 14.75 ms | 0.293 ms | 0.194 ms | 14.73 ms | 15.05 ms |  1.00 |    0.02 | 58.8235 | 599.72 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 13.86 ms | 1.183 ms | 0.783 ms | 13.81 ms | 14.78 ms |  0.94 |    0.05 | 58.8235 | 570.82 KB |        0.95 |
