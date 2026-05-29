// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using MongoDB.Driver;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB;

/// <summary>
/// Extension methods on <see cref="MongoClientSettings"/> for users who construct
/// <c>MongoClient</c> manually instead of through a DI container.
/// </summary>
public static class MongoClientSettingsExtensions
{
    /// <summary>
    /// Adds the MongoDB OpenTelemetry instrumentation event subscriber to the supplied
    /// settings by chaining onto the existing <see cref="MongoClientSettings.ClusterConfigurator"/>
    /// (any previously registered configurator is preserved).
    /// </summary>
    /// <param name="settings">The settings to mutate.</param>
    /// <param name="options">Optional instrumentation options. When <see langword="null"/>,
    /// PII-safe defaults are used.</param>
    /// <returns>The same <see cref="MongoClientSettings"/> instance for fluent chaining.</returns>
    public static MongoClientSettings AddOpenTelemetryInstrumentation(
        this MongoClientSettings settings,
        MongoDBInstrumentationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var effectiveOptions = options ?? new MongoDBInstrumentationOptions();
        var telemetry = new MongoCommandTelemetry(effectiveOptions);
        var subscriber = new MongoCommandEventSubscriber(telemetry);

        var existingConfigurator = settings.ClusterConfigurator;
        settings.ClusterConfigurator = cb =>
        {
            existingConfigurator?.Invoke(cb);
            cb.Subscribe(subscriber);
        };

        return settings;
    }

    /// <summary>
    /// Adds the MongoDB OpenTelemetry instrumentation event subscriber to the supplied
    /// settings, configuring the instrumentation options through a callback applied to a
    /// fresh <see cref="MongoDBInstrumentationOptions"/> instance pre-populated with the
    /// PII-safe defaults.
    /// </summary>
    /// <param name="settings">The settings to mutate.</param>
    /// <param name="configure">Callback invoked to mutate the default options. Required.</param>
    /// <returns>The same <see cref="MongoClientSettings"/> instance for fluent chaining.</returns>
    public static MongoClientSettings AddOpenTelemetryInstrumentation(
        this MongoClientSettings settings,
        Action<MongoDBInstrumentationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MongoDBInstrumentationOptions();
        configure(options);

        return settings.AddOpenTelemetryInstrumentation(options);
    }
}
