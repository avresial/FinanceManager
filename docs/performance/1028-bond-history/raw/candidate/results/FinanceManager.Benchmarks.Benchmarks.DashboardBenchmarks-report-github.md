```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms
WarmupCount=3

```
| Method                                  | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Gen0      | Gen1     | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|----------:|---------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 66.60 ms | 7.105 ms | 4.700 ms | 68.01 ms | 71.84 ms |  1.00 |    0.10 | 1000.0000 | 500.0000 |   6.79 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 38.07 ms | 1.476 ms | 0.977 ms | 37.80 ms | 39.54 ms |  0.57 |    0.04 |  666.6667 | 166.6667 |   4.41 MB |        0.65 |
