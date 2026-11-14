```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean      | Error    | StdDev   | Median    | P95       | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|---------------------------------------- |----------:|---------:|---------:|----------:|----------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 186.27 ms | 9.064 ms | 5.995 ms | 185.02 ms | 195.00 ms |  1.00 |    0.04 | 11000.0000 | 2000.0000 | 1000.0000 |  90.58 MB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  |  57.27 ms | 1.701 ms | 1.125 ms |  57.33 ms |  58.77 ms |  0.31 |    0.01 |  2500.0000 |  750.0000 |         - |  19.46 MB |        0.21 |
