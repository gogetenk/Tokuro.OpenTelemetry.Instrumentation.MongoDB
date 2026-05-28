# Benchmarks

BenchmarkDotNet harness for `Tokuro.OpenTelemetry.Instrumentation.MongoDB`. Focused on the two hot paths:

1. **`MongoCommandRedactor`** — BSON walk that produces `db.statement`.
2. **`MongoCommandTelemetry`** — `ActivitySource.StartActivity` lifecycle bookkeeping (no-listener, sampled-in, sampled-out, full lifecycle).

## Run

```bash
dotnet run -c Release --project bench/Tokuro.OpenTelemetry.Instrumentation.MongoDB.Benchmarks -- --filter '*'
```

Filter to a single class:

```bash
dotnet run -c Release -- --filter '*MongoCommandRedactor*'
```

## Compare against a baseline

BenchmarkDotNet emits relative ratios when one method is annotated with `[Benchmark(Baseline = true)]` (already done here — `Redact_SmallFind` and `Started_NoListener` are the baselines).

To compare against a previous run / branch, export results to JSON and diff:

```bash
git checkout main
dotnet run -c Release -- --filter '*' --exporters json --artifacts ./BenchmarkDotNet.Artifacts/main

git checkout my-branch
dotnet run -c Release -- --filter '*' --exporters json --artifacts ./BenchmarkDotNet.Artifacts/branch
```

Then point your favourite diff tool — or `dotnet tool install -g ResultsComparer` — at the two `.json` files.

## Performance bar

- **Sampled-out path** (`Started_WithListener_SampledOut`): **zero** allocations on steady state and no regression vs. previous release. This is the path that runs in production when traces aren't being collected — any allocation here multiplies by the request rate.
- **Full path** (`EndToEnd_Started_Succeeded` and all `Redact_*` cases): less than **2%** mean-time regression without a written justification in the PR.
- **`Redact_NearTruncationLimit`**: truncation should never cost more than ~10% over `Redact_DeepNested` of comparable shape.

## Example output (placeholder)

```
| Method                          | Runtime  | Mean       | Ratio | Allocated |
|-------------------------------- |--------- |-----------:|------:|----------:|
| Redact_SmallFind                | .NET 8.0 |   1.234 us |  1.00 |     312 B |
| Redact_DeepNested               | .NET 8.0 |   6.789 us |  5.50 |   1,872 B |
| Redact_LargeInsert              | .NET 8.0 |  12.345 us | 10.00 |   3,640 B |
| Redact_BulkWrite                | .NET 8.0 |  98.765 us | 80.00 |  28,400 B |
| Redact_NearTruncationLimit      | .NET 8.0 | 145.000 us |117.50 |   4,096 B |
```
