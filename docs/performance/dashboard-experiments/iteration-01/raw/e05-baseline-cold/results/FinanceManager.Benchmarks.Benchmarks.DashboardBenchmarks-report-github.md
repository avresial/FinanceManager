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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 81.44 ms | 3.338 ms | 2.208 ms | 81.19 ms | 84.69 ms |  1.00 |    0.04 | 750.0000 | 250.0000 |   5.33 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 55.62 ms | 2.500 ms | 1.654 ms | 55.24 ms | 58.14 ms |  0.68 |    0.03 | 250.0000 |        - |   2.89 MB |        0.54 |
