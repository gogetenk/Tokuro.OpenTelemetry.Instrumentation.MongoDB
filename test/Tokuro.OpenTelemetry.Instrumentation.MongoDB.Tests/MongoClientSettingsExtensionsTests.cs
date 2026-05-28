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
}
