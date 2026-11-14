```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=10  IterationTime=250ms  
WarmupCount=3  

```
| Method                                | Mean     | Error     | StdDev    | Median   | P95      | Gen0    | Allocated |
|-------------------------------------- |---------:|----------:|----------:|---------:|---------:|--------:|----------:|
| &#39;GET api/TransactionLog/Get/{userId}&#39; | 6.664 ms | 0.1100 ms | 0.0727 ms | 6.657 ms | 6.764 ms | 41.6667 | 322.87 KB |
