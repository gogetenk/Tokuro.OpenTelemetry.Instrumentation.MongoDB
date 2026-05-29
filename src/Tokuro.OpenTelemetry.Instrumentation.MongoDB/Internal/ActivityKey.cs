// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;

// Identifies an in-flight command by its connection and driver-assigned request id.
internal readonly record struct ActivityKey(long ConnectionId, int RequestId);
