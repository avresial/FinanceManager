```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean     | Error    | StdDev   | Median   | P95       | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|---------:|---------:|---------:|----------:|------:|--------:|---------:|---------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 93.22 ms | 9.272 ms | 6.133 ms | 91.59 ms | 102.64 ms |  1.00 |    0.09 | 750.0000 | 250.0000 |   5.36 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 59.97 ms | 2.157 ms | 1.128 ms | 59.94 ms |  61.37 ms |  0.65 |    0.04 | 250.0000 |        - |   2.89 MB |        0.54 |
