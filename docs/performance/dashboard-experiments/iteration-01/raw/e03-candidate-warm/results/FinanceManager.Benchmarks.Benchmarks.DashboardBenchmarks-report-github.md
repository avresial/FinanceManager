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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 85.41 ms | 8.001 ms | 5.292 ms | 86.11 ms | 91.67 ms |  1.00 |    0.08 | 750.0000 | 250.0000 |   5.33 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 54.53 ms | 2.735 ms | 1.809 ms | 54.33 ms | 57.21 ms |  0.64 |    0.04 | 250.0000 |        - |   2.85 MB |        0.53 |
