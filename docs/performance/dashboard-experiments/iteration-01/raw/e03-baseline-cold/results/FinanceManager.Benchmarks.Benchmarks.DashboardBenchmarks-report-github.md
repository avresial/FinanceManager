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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 89.10 ms | 11.236 ms | 6.686 ms | 93.53 ms | 94.57 ms |  1.01 |    0.10 | 750.0000 | 250.0000 |   5.35 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 56.35 ms |  3.164 ms | 2.093 ms | 55.76 ms | 59.57 ms |  0.64 |    0.05 | 250.0000 |        - |   2.89 MB |        0.54 |
