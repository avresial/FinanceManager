```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean     | Error     | StdDev   | Median   | P95      | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|----------:|---------:|---------:|---------:|------:|--------:|---------:|---------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 81.23 ms | 10.236 ms | 6.771 ms | 80.31 ms | 89.23 ms |  1.01 |    0.11 | 750.0000 | 250.0000 |   5.29 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 54.99 ms |  2.540 ms | 1.680 ms | 54.50 ms | 57.03 ms |  0.68 |    0.06 | 250.0000 |        - |   2.87 MB |        0.54 |
