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
| &#39;GET api/Dashboard/overview (6 months)&#39; | 183.53 ms | 10.560 ms | 6.985 ms | 183.26 ms | 192.87 ms |  1.00 |    0.05 | 11000.0000 | 2000.0000 | 1000.0000 |   90.5 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  |  56.17 ms |  0.795 ms | 0.416 ms |  56.11 ms |  56.77 ms |  0.31 |    0.01 |  2500.0000 |  500.0000 |         - |  19.47 MB |        0.22 |
