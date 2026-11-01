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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 16.22 ms | 0.263 ms | 0.157 ms | 16.13 ms | 16.39 ms |  1.00 |    0.01 | 71.4286 | 654.94 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 15.64 ms | 0.802 ms | 0.477 ms | 15.68 ms | 16.27 ms |  0.96 |    0.03 | 71.4286 | 625.92 KB |        0.96 |
