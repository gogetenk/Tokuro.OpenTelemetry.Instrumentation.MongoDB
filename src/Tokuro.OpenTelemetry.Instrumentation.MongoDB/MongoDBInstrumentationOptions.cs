// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using MongoDB.Driver.Core.Events;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB;

/// <summary>
/// Configuration options for the MongoDB OpenTelemetry instrumentation.
/// Defaults are PII-safe: command payloads are captured but every scalar value is replaced
/// with <c>"?"</c> before being attached to a span, and failure messages are suppressed.
/// </summary>
public sealed class MongoDBInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the redacted command text should be attached
    /// to spans as <c>db.statement</c> and/or <c>db.query.text</c>. Values are always redacted
    /// regardless of this flag — disabling it merely omits the attribute entirely.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool CaptureCommandText { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum length, in characters, of the redacted command text attached
    /// to a span. Text exceeding this length is truncated and suffixed with a constant marker.
    /// Must be <c>&gt;= 1</c>; values below the internal marker-plus-floor minimum are clamped
    /// upward by the redactor to keep the truncated string well-formed. Defaults to <c>4_000</c>.
    /// </summary>
    public int MaxCommandTextLength { get; set; } = 4_000;

    /// <summary>
    /// Gets or sets a value indicating whether the message of a failed command's exception
    /// should be suppressed. Driver failure messages can echo BSON fragments (duplicate-key
    /// payloads, schema-validation errors, rejected documents) which may leak user data into
    /// telemetry. Defaults to <see langword="true"/>.
    /// </summary>
    public bool SuppressExceptionMessage { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of in-flight command activities tracked at any time.
    /// Acts as a defensive upper bound to prevent unbounded growth if a terminal driver
    /// event is ever lost. Must be <c>&gt;= 1</c>. When the cap is reached, NEW commands are
    /// dropped (their activity is stopped immediately and never tracked); existing in-flight
    /// activities are preserved. An EventSource counter is emitted on each drop so operators
    /// can detect the condition. Defaults to <c>10_000</c>.
    /// </summary>
    public int MaxInFlightCommands { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets a value indicating whether legacy OpenTelemetry database semantic
    /// convention attributes (<c>db.system</c>, <c>db.statement</c>, <c>db.name</c>) should
    /// be emitted. Defaults to <see langword="true"/> for backward compatibility.
    /// </summary>
    public bool EmitLegacyAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether stable OpenTelemetry database semantic
    /// convention attributes (<c>db.system.name</c>, <c>db.query.text</c>,
    /// <c>db.namespace</c>) should be emitted. Defaults to <see langword="true"/>.
    /// </summary>
    public bool EmitStableAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional predicate that decides whether a given
    /// <see cref="CommandStartedEvent"/> should be instrumented. Return <see langword="false"/>
    /// to skip the command entirely (no activity is started). When <see langword="null"/>,
    /// only the built-in handshake/heartbeat command list is filtered out.
    /// </summary>
    public Func<CommandStartedEvent, bool>? FilterCommand { get; set; }
}
