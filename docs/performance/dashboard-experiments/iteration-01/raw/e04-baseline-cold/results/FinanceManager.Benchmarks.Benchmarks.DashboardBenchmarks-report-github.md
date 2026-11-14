```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|---------:|---------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 82.49 ms | 6.547 ms | 4.330 ms | 83.50 ms | 87.14 ms |  1.00 |    0.07 | 750.0000 | 250.0000 |   5.32 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 56.04 ms | 3.509 ms | 2.321 ms | 55.86 ms | 59.45 ms |  0.68 |    0.04 | 250.0000 |        - |   2.89 MB |        0.54 |
