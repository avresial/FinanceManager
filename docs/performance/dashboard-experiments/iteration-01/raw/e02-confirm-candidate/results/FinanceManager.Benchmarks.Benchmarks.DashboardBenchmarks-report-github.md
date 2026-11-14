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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 103.26 ms | 5.847 ms | 3.058 ms | 104.22 ms | 105.85 ms |  1.00 |    0.04 | 500.0000 |   5.86 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  |  66.65 ms | 3.439 ms | 2.275 ms |  66.55 ms |  69.50 ms |  0.65 |    0.03 | 500.0000 |   3.27 MB |        0.56 |
