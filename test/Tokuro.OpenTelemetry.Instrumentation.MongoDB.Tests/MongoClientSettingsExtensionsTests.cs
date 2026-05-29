// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests;

public sealed class MongoClientSettingsExtensionsTests
{
    [Fact]
    public void AddOpenTelemetryInstrumentation_AttachesClusterConfigurator()
    {
        var settings = new MongoClientSettings();
        settings.ClusterConfigurator.Should().BeNull();

        settings.AddOpenTelemetryInstrumentation();

        settings.ClusterConfigurator.Should().NotBeNull();
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_DoesNotReplaceExistingConfigurator()
    {
        var existingInvoked = false;
        var settings = new MongoClientSettings
        {
            ClusterConfigurator = _ => existingInvoked = true
        };
        var originalConfigurator = settings.ClusterConfigurator;

        settings.AddOpenTelemetryInstrumentation();

        settings.ClusterConfigurator.Should().NotBeNull();
        settings.ClusterConfigurator.Should().NotBeSameAs(originalConfigurator);
        var builder = new ClusterBuilder();
        settings.ClusterConfigurator!(builder);
        existingInvoked.Should().BeTrue("the previously attached configurator must still be invoked");
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_WithConfigureCallback_AttachesConfiguratorAndAppliesOptions()
    {
        var settings = new MongoClientSettings();
        var captured = new MongoDBInstrumentationOptions();

        settings.AddOpenTelemetryInstrumentation(options =>
        {
            options.CaptureCommandText = false;
            options.MaxCommandTextLength = 1_234;
            captured = options;
        });

        settings.ClusterConfigurator.Should().NotBeNull();
        captured.CaptureCommandText.Should().BeFalse();
        captured.MaxCommandTextLength.Should().Be(1_234);
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_WithNullConfigureCallback_Throws()
    {
        var settings = new MongoClientSettings();

        var act = () => settings.AddOpenTelemetryInstrumentation((Action<MongoDBInstrumentationOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
