```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                  | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|--------:|----------:|------------:|
| &#39;GET api/Dashboard/overview (6 months)&#39; | 17.38 ms | 0.426 ms | 0.253 ms | 17.35 ms | 17.77 ms |  1.00 |    0.02 | 66.6667 | 646.33 KB |        1.00 |
| &#39;GET api/Dashboard/overview (1 month)&#39;  | 14.05 ms | 1.282 ms | 0.763 ms | 13.88 ms | 15.06 ms |  0.81 |    0.04 | 66.6667 | 616.29 KB |        0.95 |
