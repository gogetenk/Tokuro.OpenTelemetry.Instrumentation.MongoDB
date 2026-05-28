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
    public void AddMongoDBInstrumentation_AppliesConfigureCallback()
    {
        MongoDBInstrumentationOptions? captured = null;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddMongoDBInstrumentation(opts =>
            {
                opts.CaptureCommandText = false;
                opts.MaxCommandTextLength = 256;
                opts.SuppressExceptionMessage = false;
                opts.EmitLegacyAttributes = false;
                opts.EmitStableAttributes = true;
                captured = opts;
            })
            .Build();

        captured.Should().NotBeNull();
        captured!.CaptureCommandText.Should().BeFalse();
        captured.MaxCommandTextLength.Should().Be(256);
        captured.SuppressExceptionMessage.Should().BeFalse();
        captured.EmitLegacyAttributes.Should().BeFalse();
        captured.EmitStableAttributes.Should().BeTrue();
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
