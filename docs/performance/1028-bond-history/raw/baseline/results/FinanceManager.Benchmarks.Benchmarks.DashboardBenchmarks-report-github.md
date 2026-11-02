```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms
WarmupCount=3

```
| Method                                  | Mean      | Error     | StdDev   | Median    | P95       | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|---------------------------------------- |----------:|----------:|---------:|----------:|----------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 182.41 ms | 14.622 ms | 9.672 ms | 184.67 ms | 193.04 ms |  1.00 |    0.07 | 11000.0000 | 2000.0000 | 1000.0000 |  90.29 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  |  56.80 ms |  2.092 ms | 1.383 ms |  56.96 ms |  58.77 ms |  0.31 |    0.02 |  2500.0000 |  500.0000 |         - |  19.39 MB |        0.21 |
