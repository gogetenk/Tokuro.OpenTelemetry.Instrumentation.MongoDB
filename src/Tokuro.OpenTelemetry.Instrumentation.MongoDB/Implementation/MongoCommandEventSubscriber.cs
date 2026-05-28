// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using MongoDB.Driver.Core.Events;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

/// <summary>
/// MongoDB driver <see cref="IEventSubscriber"/> that forwards command lifecycle events
/// (started / succeeded / failed) to a single <see cref="MongoCommandTelemetry"/> instance.
/// One subscriber per <c>MongoClient</c> — wire it through
/// <c>MongoClientSettings.ClusterConfigurator</c>.
/// </summary>
internal sealed class MongoCommandEventSubscriber : IEventSubscriber
{
    private readonly MongoCommandTelemetry _telemetry;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoCommandEventSubscriber"/> class.
    /// </summary>
    /// <param name="telemetry">The telemetry sink that will receive command events.</param>
    public MongoCommandEventSubscriber(MongoCommandTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public bool TryGetEventHandler<TEvent>(out Action<TEvent> handler)
    {
        if (typeof(TEvent) == typeof(CommandStartedEvent))
        {
            handler = @event => _telemetry.Started((CommandStartedEvent)(object)@event!);
            return true;
        }

        if (typeof(TEvent) == typeof(CommandSucceededEvent))
        {
            handler = @event => _telemetry.Succeeded((CommandSucceededEvent)(object)@event!);
            return true;
        }

        if (typeof(TEvent) == typeof(CommandFailedEvent))
        {
            handler = @event => _telemetry.Failed((CommandFailedEvent)(object)@event!);
            return true;
        }

        handler = null!;
        return false;
    }
}
