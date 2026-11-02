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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 16.14 ms | 0.987 ms | 0.653 ms | 16.13 ms | 16.91 ms |  1.00 |    0.05 | 62.5000 | 610.83 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 14.43 ms | 1.328 ms | 0.879 ms | 14.79 ms | 15.27 ms |  0.90 |    0.06 | 62.5000 | 582.29 KB |        0.95 |
