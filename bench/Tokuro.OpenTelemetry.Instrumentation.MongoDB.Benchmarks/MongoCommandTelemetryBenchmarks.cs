// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MongoDB.Bson;
using MongoDB.Driver.Core.Events;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Benchmarks.Internal;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class MongoCommandTelemetryBenchmarks
{
    private static readonly BsonDocument SampleCommand = BsonDocument.Parse("""
    {
      "find": "users",
      "filter": { "tenantId": "acme", "status": "active" },
      "limit": 10,
      "$db": "app"
    }
    """);

    private MongoCommandTelemetry _telemetry = null!;
    private ActivityListener? _sampledInListener;
    private ActivityListener? _sampledOutListener;

    [GlobalSetup(Target = nameof(Started_NoListener))]
    public void SetupNoListener()
    {
        _telemetry = new MongoCommandTelemetry(new MongoDBInstrumentationOptions());
    }

    [GlobalSetup(Targets = new[] { nameof(Started_WithListener_SampledIn), nameof(EndToEnd_Started_Succeeded) })]
    public void SetupSampledIn()
    {
        _telemetry = new MongoCommandTelemetry(new MongoDBInstrumentationOptions());
        _sampledInListener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == TracerProviderBuilderExtensions.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_sampledInListener);
    }

    [GlobalSetup(Target = nameof(Started_WithListener_SampledOut))]
    public void SetupSampledOut()
    {
        _telemetry = new MongoCommandTelemetry(new MongoDBInstrumentationOptions());
        _sampledOutListener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == TracerProviderBuilderExtensions.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.PropagationData,
        };
        ActivitySource.AddActivityListener(_sampledOutListener);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sampledInListener?.Dispose();
        _sampledOutListener?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void Started_NoListener()
    {
        _telemetry.Started(CommandEventFactory.Started(command: SampleCommand, requestId: 1));
        _telemetry.Succeeded(CommandEventFactory.Succeeded(requestId: 1));
    }

    [Benchmark]
    public void Started_WithListener_SampledIn()
    {
        _telemetry.Started(CommandEventFactory.Started(command: SampleCommand, requestId: 2));
        _telemetry.Succeeded(CommandEventFactory.Succeeded(requestId: 2));
    }

    [Benchmark]
    public void Started_WithListener_SampledOut()
    {
        _telemetry.Started(CommandEventFactory.Started(command: SampleCommand, requestId: 3));
        _telemetry.Succeeded(CommandEventFactory.Succeeded(requestId: 3));
    }

    [Benchmark]
    public void EndToEnd_Started_Succeeded()
    {
        _telemetry.Started(CommandEventFactory.Started(command: SampleCommand, requestId: 4));
        _telemetry.Succeeded(CommandEventFactory.Succeeded(requestId: 4));
    }
}
