// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver.Core.Events;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

// Translates MongoDB driver command events into OpenTelemetry spans. Event entry points
// swallow all exceptions: the driver invokes subscribers synchronously on its I/O thread,
// so a throw here would crash that thread and surface in user code.
internal sealed class MongoCommandTelemetry
{
    internal static readonly ActivitySource ActivitySource = new(TracerProviderBuilderExtensions.ActivitySourceName);

    private static readonly HashSet<string> _ignoredCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "buildInfo",
        "endSessions",
        "hello",
        "isMaster",
        "saslContinue",
        "saslStart",
    };

    private readonly ConcurrentDictionary<ActivityKey, Activity> _activities = new();
    private readonly object _overflowLock = new();
    private readonly MongoCommandRedactor _redactor;
    private readonly MongoDBInstrumentationOptions _options;

    public MongoCommandTelemetry(MongoDBInstrumentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxInFlightCommands, 1, nameof(options.MaxInFlightCommands));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxCommandTextLength, 1, nameof(options.MaxCommandTextLength));

        _options = options;
        _redactor = new MongoCommandRedactor(options.MaxCommandTextLength);
    }

    public void Started(CommandStartedEvent @event)
    {
        try
        {
            if (_ignoredCommands.Contains(@event.CommandName))
            {
                return;
            }

            if (_options.FilterCommand is { } filter && !filter(@event))
            {
                return;
            }

            var collectionName = TryGetCollectionName(@event.Command, @event.CommandName);
            var activity = ActivitySource.StartActivity(
                GetActivityName(@event.CommandName, collectionName),
                ActivityKind.Client);

            if (activity is null)
            {
                return;
            }

            if (activity.IsAllDataRequested)
            {
                PopulateTags(activity, @event, collectionName);
            }

            // A null ConnectionId cannot form a sound (connection, request) key, and the
            // matching terminal events lack it too, so the activity could never be stopped.
            // Stop it now rather than leaking it into the dictionary.
            if (@event.ConnectionId is null)
            {
                activity.Stop();
                return;
            }

            TrackActivity(@event.ConnectionId.LongLocalValue, @event.RequestId, activity);
        }
        catch (Exception)
        {
            // Telemetry must never crash the driver thread.
        }
    }

    public void Succeeded(CommandSucceededEvent @event)
    {
        try
        {
            if (@event.ConnectionId is null)
            {
                return;
            }

            var key = new ActivityKey(@event.ConnectionId.LongLocalValue, @event.RequestId);
            if (_activities.TryRemove(key, out var activity))
            {
                activity.SetStatus(ActivityStatusCode.Ok);
                activity.Stop();
            }
        }
        catch (Exception)
        {
            // Telemetry must never crash the driver thread.
        }
    }

    public void Failed(CommandFailedEvent @event)
    {
        try
        {
            if (@event.ConnectionId is null)
            {
                return;
            }

            var key = new ActivityKey(@event.ConnectionId.LongLocalValue, @event.RequestId);
            if (!_activities.TryRemove(key, out var activity))
            {
                return;
            }

            activity.SetStatus(ActivityStatusCode.Error);

            var failureType = @event.Failure.GetType();
            var errorTypeName = failureType.FullName ?? failureType.Name;
            activity.SetTag(SemConv.ErrorType, errorTypeName);

            // OTel reports exception detail on an "exception" span event, not span attributes.
            // The message is opt-in only, as driver failures can echo BSON fragments.
            if (!_options.SuppressExceptionMessage)
            {
                var exceptionTags = new ActivityTagsCollection
                {
                    { SemConv.ExceptionType, errorTypeName },
                    { SemConv.ExceptionMessage, @event.Failure.Message },
                };
                activity.AddEvent(new ActivityEvent(SemConv.ExceptionEventName, tags: exceptionTags));
            }

            activity.Stop();
        }
        catch (Exception)
        {
            // Telemetry must never crash the driver thread.
        }
    }

    private void PopulateTags(Activity activity, CommandStartedEvent @event, string? collectionName)
    {
        var databaseName = @event.DatabaseNamespace.DatabaseName;
        string? redactedStatement = _options.CaptureCommandText ? _redactor.Redact(@event.Command) : null;

        if (_options.EmitLegacyAttributes)
        {
            activity.SetTag(SemConv.DbSystem, SemConv.MongoDbSystem);
            activity.SetTag(SemConv.DbName, databaseName);
            activity.SetTag(SemConv.DbOperation, @event.CommandName);
            if (redactedStatement is not null)
            {
                activity.SetTag(SemConv.DbStatement, redactedStatement);
            }
        }

        if (_options.EmitStableAttributes)
        {
            activity.SetTag(SemConv.DbSystemName, SemConv.MongoDbSystem);
            activity.SetTag(SemConv.DbNamespace, databaseName);
            activity.SetTag(SemConv.DbOperationName, @event.CommandName);
            if (redactedStatement is not null)
            {
                activity.SetTag(SemConv.DbQueryText, redactedStatement);
            }
        }

        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            activity.SetTag(SemConv.DbCollectionName, collectionName);
        }

        SetServerTags(activity, @event.ConnectionId?.ServerId?.EndPoint);
    }

    private void TrackActivity(long connectionId, int requestId, Activity activity)
    {
        // Drop-new on overflow: refuse the new activity rather than evict healthy in-flight
        // spans. The lock-then-recheck closes the check-then-act race between threads.
        if (_activities.Count >= _options.MaxInFlightCommands)
        {
            lock (_overflowLock)
            {
                if (_activities.Count >= _options.MaxInFlightCommands)
                {
                    TelemetryEventSource.Log.ActivityEvicted(_activities.Count);
                    activity.Stop();
                    return;
                }
            }
        }

        _activities[new ActivityKey(connectionId, requestId)] = activity;
    }

    private static void SetServerTags(Activity activity, EndPoint? endpoint)
    {
        switch (endpoint)
        {
            case null:
                return;
            case DnsEndPoint dns:
                activity.SetTag(SemConv.ServerAddress, dns.Host);
                activity.SetTag(SemConv.ServerPort, dns.Port);
                break;
            case IPEndPoint ip:
                activity.SetTag(SemConv.ServerAddress, ip.Address.ToString());
                activity.SetTag(SemConv.ServerPort, ip.Port);
                break;
            case UnixDomainSocketEndPoint uds:
                activity.SetTag(SemConv.ServerAddress, uds.ToString());
                break;
            default:
                activity.SetTag(SemConv.ServerAddress, endpoint.ToString());
                break;
        }
    }

    private static string GetActivityName(string commandName, string? collectionName)
        => string.IsNullOrWhiteSpace(collectionName)
            ? "MongoDB " + commandName
            : "MongoDB " + commandName + " " + collectionName;

    private static string? TryGetCollectionName(BsonDocument command, string commandName)
        => command.TryGetValue(commandName, out var value) && value.IsString
            ? value.AsString
            : null;

    [EventSource(Name = "Tokuro-OpenTelemetry-Instrumentation-MongoDB")]
    internal sealed class TelemetryEventSource : EventSource
    {
        internal static readonly TelemetryEventSource Log = new();

        private TelemetryEventSource()
        {
        }

        [Event(1, Level = EventLevel.Warning, Message = "MongoDB instrumentation dropped a new activity; in-flight cap reached ({0}).")]
        public void ActivityEvicted(long inFlightCount) => WriteEvent(1, inFlightCount);
    }
}
