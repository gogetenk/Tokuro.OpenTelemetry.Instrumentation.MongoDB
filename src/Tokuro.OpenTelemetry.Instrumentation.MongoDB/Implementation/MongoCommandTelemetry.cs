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

/// <summary>
/// Owns the in-flight <see cref="Activity"/> dictionary, the singleton
/// <see cref="ActivitySource"/>, and the per-event tag-set logic. Translates MongoDB
/// driver command events into OpenTelemetry spans, dual-writing legacy and stable
/// database semantic convention attributes.
/// <para>
/// Design note: every public event entry point catches and swallows all exceptions.
/// The MongoDB driver invokes subscribers synchronously on its I/O thread; a throw from
/// telemetry would crash the driver thread and propagate to user code. Telemetry must
/// never crash the host.
/// </para>
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
    private readonly object _overflowLock = new();
    private readonly MongoCommandRedactor _redactor;
    private readonly MongoDBInstrumentationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoCommandTelemetry"/> class.
    /// </summary>
    /// <param name="options">Effective instrumentation options (non-null).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="MongoDBInstrumentationOptions.MaxInFlightCommands"/> or
    /// <see cref="MongoDBInstrumentationOptions.MaxCommandTextLength"/> is less than 1.
    /// </exception>
    public MongoCommandTelemetry(MongoDBInstrumentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxInFlightCommands, 1, nameof(options.MaxInFlightCommands));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxCommandTextLength, 1, nameof(options.MaxCommandTextLength));

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
        // Telemetry must not crash the driver thread. Swallow all exceptions.
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

            // When ConnectionId is null we cannot build a sound (connection, request) key:
            // two distinct unknown-connection commands sharing a RequestId would collide on
            // (-1, requestId) and one would leak. The matching Succeeded/Failed events also
            // lack ConnectionId in this scenario, so skipping the dictionary insert here is
            // symmetric (no leak — just no Stop driven by the terminal event). Stop the
            // activity immediately so it isn't left dangling.
            if (@event.ConnectionId is null)
            {
                activity.Stop();
                return;
            }

            TrackActivity(@event.ConnectionId.LongLocalValue, @event.RequestId, activity);
        }
        catch (Exception)
        {
            // Intentionally swallowed — see class-level design note.
        }
    }

    /// <summary>
    /// Handles a <see cref="CommandSucceededEvent"/>: marks the matching activity as
    /// <see cref="ActivityStatusCode.Ok"/> and stops it.
    /// </summary>
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
            // Intentionally swallowed — see class-level design note.
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

            // error.type per OTel sem-conv must be non-empty; FullName can be null for some
            // generated/dynamic types, so fall back to the short name in that case.
            var failureType = @event.Failure.GetType();
            activity.SetTag(SemConv.ErrorType, failureType.FullName ?? failureType.Name);

            if (!_options.SuppressExceptionMessage)
            {
                activity.SetTag(SemConv.ExceptionMessage, @event.Failure.Message);
            }

            activity.Stop();
        }
        catch (Exception)
        {
            // Intentionally swallowed — see class-level design note.
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
        // Drop-new semantics: when the cap is reached we refuse the NEW activity rather than
        // evicting healthy in-flight spans. Lock-then-recheck closes the TOCTOU window where
        // multiple threads could otherwise observe count == cap and each attempt eviction.
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
                // UDS deployments have no port; record only the socket path.
                activity.SetTag(SemConv.ServerAddress, uds.ToString());
                break;
            default:
                // Unknown endpoint kind: best-effort string representation as server.address,
                // no server.port (we cannot infer one safely).
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

    /// <summary>
    /// EventSource that surfaces operational signals from this instrumentation. Consumers
    /// (PerfView, dotnet-trace, in-process listeners) can subscribe to detect overflow
    /// conditions such as in-flight cap evictions without depending on logging plumbing.
    /// </summary>
    [EventSource(Name = "Tokuro-OpenTelemetry-Instrumentation-MongoDB")]
    internal sealed class TelemetryEventSource : EventSource
    {
        internal static readonly TelemetryEventSource Log = new();

        private TelemetryEventSource()
        {
        }

        /// <summary>
        /// Emitted when the in-flight activity cap is hit and a new command activity is
        /// dropped rather than tracked. The argument is the current in-flight count at the
        /// moment of the drop.
        /// </summary>
        /// <param name="inFlightCount">Current size of the in-flight activity dictionary.</param>
        [Event(1, Level = EventLevel.Warning, Message = "MongoDB instrumentation dropped a new activity; in-flight cap reached ({0}).")]
        public void ActivityEvicted(long inFlightCount) => WriteEvent(1, inFlightCount);
    }
}
