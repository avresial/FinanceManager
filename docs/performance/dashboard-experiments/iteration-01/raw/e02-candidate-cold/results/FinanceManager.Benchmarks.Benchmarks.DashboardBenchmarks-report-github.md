```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean      | Error    | StdDev   | Median    | P95       | Ratio | RatioSD | Gen0     | Allocated | Alloc Ratio |
|---------------------------------------- |----------:|---------:|---------:|----------:|----------:|------:|--------:|---------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 100.23 ms | 4.349 ms | 2.877 ms | 100.88 ms | 103.65 ms |  1.00 |    0.04 | 500.0000 |   5.93 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  |  67.35 ms | 3.786 ms | 2.504 ms |  67.42 ms |  70.47 ms |  0.67 |    0.03 | 500.0000 |   3.27 MB |        0.55 |
