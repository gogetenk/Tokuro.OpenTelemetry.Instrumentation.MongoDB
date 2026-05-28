// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;

/// <summary>
/// Composite key uniquely identifying an in-flight MongoDB command activity by the
/// connection that issued it and the driver-assigned request id. Used as the dictionary
/// key for the bounded in-flight activity map.
/// </summary>
/// <param name="ConnectionId">The driver's local connection identifier (<c>-1</c> when unknown).</param>
/// <param name="RequestId">The driver-assigned request identifier for the command.</param>
internal readonly record struct ActivityKey(long ConnectionId, int RequestId);
