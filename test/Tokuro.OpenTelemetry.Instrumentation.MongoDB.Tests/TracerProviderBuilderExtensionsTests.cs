// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests;

public sealed class TracerProviderBuilderExtensionsTests
{
    private const string ActivitySourceName = "Tokuro.OpenTelemetry.Instrumentation.MongoDB";

    [Fact]
    public void AddMongoDBInstrumentation_RegistersActivitySource()
    {
        var exportedActivities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddMongoDBInstrumentation()
            .AddInMemoryExporter(exportedActivities)
            .Build();

        using var source = new ActivitySource(ActivitySourceName);
        using var activity = source.StartActivity("probe");
        activity.Should().NotBeNull("the ActivitySource should be sampled when AddMongoDBInstrumentation registers it");
    }

    [Fact]
    public void MongoDBInstrumentationOptions_Defaults_AreSafe()
    {
        // The configure callback overload was intentionally NOT added to the tracer-provider
        // extension because options only take effect when consumed by MongoClientSettings
        // .AddOpenTelemetryInstrumentation. Verify the defaults instead — those are the contract.
        var defaults = new MongoDBInstrumentationOptions();

        defaults.CaptureCommandText.Should().BeTrue();
        defaults.MaxCommandTextLength.Should().Be(4_000);
        defaults.SuppressExceptionMessage.Should().BeTrue();
        defaults.MaxInFlightCommands.Should().Be(10_000);
        defaults.EmitLegacyAttributes.Should().BeTrue();
        defaults.EmitStableAttributes.Should().BeTrue();
        defaults.FilterCommand.Should().BeNull();
    }

    [Fact]
    public void AddMongoDBInstrumentation_WithoutConfigure_UsesDefaults()
    {
        var options = new MongoDBInstrumentationOptions();

        options.CaptureCommandText.Should().BeTrue();
        options.SuppressExceptionMessage.Should().BeTrue();
        options.MaxInFlightCommands.Should().Be(10_000);
        options.MaxCommandTextLength.Should().Be(4_000);
        options.EmitLegacyAttributes.Should().BeTrue();
        options.EmitStableAttributes.Should().BeTrue();
        options.FilterCommand.Should().BeNull();
    }
}
