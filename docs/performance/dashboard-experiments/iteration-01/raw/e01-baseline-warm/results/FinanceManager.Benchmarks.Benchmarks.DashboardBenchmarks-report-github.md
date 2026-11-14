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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 15.73 ms | 0.355 ms | 0.235 ms | 15.76 ms | 16.07 ms |  1.00 |    0.02 | 58.8235 | 645.46 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 13.84 ms | 1.176 ms | 0.778 ms | 13.66 ms | 15.12 ms |  0.88 |    0.05 | 58.8235 | 615.31 KB |        0.95 |
