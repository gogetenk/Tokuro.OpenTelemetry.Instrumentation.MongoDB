// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using MongoDB.Bson;
using MongoDB.Driver.Core.Events;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

/// <summary>
/// Owns the in-flight <see cref="Activity"/> dictionary, the singleton
/// <see cref="ActivitySource"/>, and the per-event tag-set logic. Translates MongoDB
/// driver command events into OpenTelemetry spans, dual-writing legacy and stable
/// database semantic convention attributes.
/// </summary>
internal sealed class MongoCommandTelemetry
{
    /// <summary>
    /// Singleton <see cref="ActivitySource"/> used by every telemetry instance in the
    /// AppDomain. Created lazily on type load and never disposed — its lifetime is the
    /// AppDomain itself, which matches the OpenTelemetry guidance for instrumentation
    /// libraries.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(TracerProviderBuilderExtensions.ActivitySourceName);

    private static readonly HashSet<string> _ignoredCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "buildInfo",
        "endSessions",
        "hello",
        "isMaster",
        "ismaster",
        "saslContinue",
        "saslStart",
    };

    private readonly ConcurrentDictionary<ActivityKey, Activity> _activities = new();
    private readonly MongoCommandRedactor _redactor;
    private readonly MongoDBInstrumentationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoCommandTelemetry"/> class.
    /// </summary>
    /// <param name="options">Effective instrumentation options (non-null).</param>
    public MongoCommandTelemetry(MongoDBInstrumentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _redactor = new MongoCommandRedactor(options.MaxCommandTextLength);
    }

    /// <summary>
    /// Handles a <see cref="CommandStartedEvent"/>: starts a client-kind activity, attaches
    /// database semantic convention tags (gated on <see cref="Activity.IsAllDataRequested"/>),
    /// and tracks the activity in the bounded dictionary keyed by connection and request id.
    /// </summary>
    public void Started(CommandStartedEvent @event)
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

        TrackActivity(@event.ConnectionId?.LongLocalValue ?? -1, @event.RequestId, activity);
    }

    /// <summary>
    /// Handles a <see cref="CommandSucceededEvent"/>: marks the matching activity as
    /// <see cref="ActivityStatusCode.Ok"/> and stops it.
    /// </summary>
    public void Succeeded(CommandSucceededEvent @event)
    {
        var key = new ActivityKey(@event.ConnectionId?.LongLocalValue ?? -1, @event.RequestId);
        if (_activities.TryRemove(key, out var activity))
        {
            activity.SetStatus(ActivityStatusCode.Ok);
            activity.Stop();
        }
    }

    /// <summary>
    /// Handles a <see cref="CommandFailedEvent"/>: marks the matching activity as
    /// <see cref="ActivityStatusCode.Error"/> and records the exception type. The exception
    /// message is suppressed by default because driver failures can echo BSON fragments
    /// containing user data.
    /// </summary>
    public void Failed(CommandFailedEvent @event)
    {
        var key = new ActivityKey(@event.ConnectionId?.LongLocalValue ?? -1, @event.RequestId);
        if (!_activities.TryRemove(key, out var activity))
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error);
        activity.SetTag(SemConv.ErrorType, @event.Failure.GetType().FullName);

        if (!_options.SuppressExceptionMessage)
        {
            activity.SetTag("exception.message", @event.Failure.Message);
        }

        activity.Stop();
    }

    private void PopulateTags(Activity activity, CommandStartedEvent @event, string? collectionName)
    {
        var databaseName = @event.DatabaseNamespace.DatabaseName;
        string? redactedStatement = _options.CaptureCommandText ? _redactor.Redact(@event.Command) : null;

        activity.SetTag(SemConv.SpanType, SemConv.MongoDbSpanType);
        activity.SetTag(SemConv.DbOperationName, @event.CommandName);

        if (_options.EmitLegacyAttributes)
        {
            activity.SetTag(SemConv.DbSystem, SemConv.MongoDbSystem);
            activity.SetTag(SemConv.DbName, databaseName);
            if (redactedStatement is not null)
            {
                activity.SetTag(SemConv.DbStatement, redactedStatement);
            }
        }

        if (_options.EmitStableAttributes)
        {
            activity.SetTag(SemConv.DbSystemName, SemConv.MongoDbSystem);
            activity.SetTag(SemConv.DbNamespace, databaseName);
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
        if (_activities.Count >= _options.MaxInFlightCommands)
        {
            // Defensive cap: if a terminal driver event were ever dropped, entries would
            // leak. Stop any in-flight activity and clear before exceeding the bound.
            foreach (var stale in _activities.Values)
            {
                stale.Stop();
            }

            _activities.Clear();
        }

        _activities[new ActivityKey(connectionId, requestId)] = activity;
    }

    private static void SetServerTags(Activity activity, EndPoint? endpoint)
    {
        switch (endpoint)
        {
            case DnsEndPoint dns:
                activity.SetTag(SemConv.ServerAddress, dns.Host);
                activity.SetTag(SemConv.ServerPort, dns.Port);
                break;
            case IPEndPoint ip:
                activity.SetTag(SemConv.ServerAddress, ip.Address.ToString());
                activity.SetTag(SemConv.ServerPort, ip.Port);
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
}
