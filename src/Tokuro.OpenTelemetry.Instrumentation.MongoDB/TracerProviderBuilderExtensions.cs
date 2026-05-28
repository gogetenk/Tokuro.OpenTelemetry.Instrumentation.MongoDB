// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using global::OpenTelemetry.Trace;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB;

/// <summary>
/// Extension methods on <see cref="TracerProviderBuilder"/> for enabling MongoDB
/// command instrumentation. Registering the instrumentation only adds the
/// <see cref="ActivitySourceName"/> to the tracer provider — the actual wiring into a
/// <c>MongoClient</c> happens at client construction time via
/// <see cref="MongoClientSettingsExtensions.AddOpenTelemetryInstrumentation"/>.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Name of the <see cref="System.Diagnostics.ActivitySource"/> used by this
    /// instrumentation. Listeners (including the OpenTelemetry tracer provider) must
    /// subscribe to this source to receive MongoDB command spans.
    /// </summary>
    public const string ActivitySourceName = "Tokuro.OpenTelemetry.Instrumentation.MongoDB";

    /// <summary>
    /// Enables MongoDB instrumentation on the supplied <see cref="TracerProviderBuilder"/>
    /// by subscribing to the <see cref="ActivitySourceName"/> activity source.
    /// </summary>
    /// <param name="builder">The tracer provider builder to extend.</param>
    /// <param name="configure">Optional callback to customize the instrumentation options.
    /// The configured options object is currently consumed at the call site only — to
    /// affect a specific <c>MongoClient</c>, pass options to
    /// <see cref="MongoClientSettingsExtensions.AddOpenTelemetryInstrumentation"/>.</param>
    /// <returns>The same <see cref="TracerProviderBuilder"/> for fluent chaining.</returns>
    public static TracerProviderBuilder AddMongoDBInstrumentation(
        this TracerProviderBuilder builder,
        Action<MongoDBInstrumentationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Materialize and validate options even if no consumer reads them here; this
        // surfaces user-side configuration errors early.
        var options = new MongoDBInstrumentationOptions();
        configure?.Invoke(options);

        return builder.AddSource(ActivitySourceName);
    }
}
