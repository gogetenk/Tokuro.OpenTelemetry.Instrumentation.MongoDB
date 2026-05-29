// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using global::OpenTelemetry.Trace;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB;

/// <summary>
/// Extension methods on <see cref="TracerProviderBuilder"/> for enabling MongoDB
/// command instrumentation. Registering the instrumentation only subscribes the
/// tracer provider to the <see cref="ActivitySourceName"/> activity source — the
/// actual wiring into a <c>MongoClient</c> happens at client construction time via
/// <see cref="MongoClientSettingsExtensions.AddOpenTelemetryInstrumentation"/>.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Name of the <see cref="System.Diagnostics.ActivitySource"/> used by this
    /// instrumentation. Exposed as a <see langword="public"/> constant so callers may
    /// subscribe manually via <c>TracerProviderBuilder.AddSource(...)</c> if they prefer.
    /// </summary>
    public const string ActivitySourceName = "Tokuro.OpenTelemetry.Instrumentation.MongoDB";

    /// <summary>
    /// Enables MongoDB instrumentation on the supplied <see cref="TracerProviderBuilder"/>
    /// by subscribing to the <see cref="ActivitySourceName"/> activity source.
    /// <para>
    /// This method registers the OpenTelemetry <see cref="System.Diagnostics.ActivitySource"/>
    /// only. Configure instrumentation options (redaction, exception-message suppression,
    /// max in-flight commands, etc.) on
    /// <see cref="MongoClientSettingsExtensions.AddOpenTelemetryInstrumentation"/> at the
    /// call site that constructs your <c>MongoClient</c>. Options passed to a tracer-provider
    /// extension would have no way to reach the driver-level event subscriber and so are
    /// intentionally not accepted here.
    /// </para>
    /// </summary>
    /// <param name="builder">The tracer provider builder to extend.</param>
    /// <returns>The same <see cref="TracerProviderBuilder"/> for fluent chaining.</returns>
    public static TracerProviderBuilder AddMongoDBInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(ActivitySourceName);
    }
}
