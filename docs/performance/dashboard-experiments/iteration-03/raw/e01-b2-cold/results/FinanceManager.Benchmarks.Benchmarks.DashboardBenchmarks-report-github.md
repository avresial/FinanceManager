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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 71.08 ms | 3.906 ms | 2.584 ms | 71.88 ms | 74.29 ms |  1.00 |    0.05 | 1000.0000 | 250.0000 |   6.94 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 38.25 ms | 0.852 ms | 0.507 ms | 38.35 ms | 38.83 ms |  0.54 |    0.02 |  666.6667 | 166.6667 |   4.48 MB |        0.65 |
