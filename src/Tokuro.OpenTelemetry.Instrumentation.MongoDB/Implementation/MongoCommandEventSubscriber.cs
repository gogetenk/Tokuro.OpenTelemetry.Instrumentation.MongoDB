// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using MongoDB.Driver.Core.Events;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

// Forwards MongoDB driver command lifecycle events to a single telemetry instance.
// One subscriber per MongoClient, wired through MongoClientSettings.ClusterConfigurator.
internal sealed class MongoCommandEventSubscriber : IEventSubscriber
{
    private readonly MongoCommandTelemetry _telemetry;

    public MongoCommandEventSubscriber(MongoCommandTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _telemetry = telemetry;
    }

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
