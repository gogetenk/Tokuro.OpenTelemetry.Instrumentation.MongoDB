// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;

/// <summary>
/// Single source of truth for OpenTelemetry attribute names used by this instrumentation.
/// Split between legacy database semantic conventions (still recognized by most backends)
/// and the current stable conventions defined by the OpenTelemetry specification.
/// </summary>
internal static class SemConv
{
    // Legacy database attributes (kept for backward compatibility with older backends).
    internal const string DbSystem = "db.system";
    internal const string DbStatement = "db.statement";
    internal const string DbName = "db.name";
    internal const string DbOperation = "db.operation";

    // Stable database attributes (current OTel semantic conventions).
    internal const string DbSystemName = "db.system.name";
    internal const string DbQueryText = "db.query.text";
    internal const string DbNamespace = "db.namespace";
    internal const string DbOperationName = "db.operation.name";
    internal const string DbCollectionName = "db.collection.name";

    // Network / server attributes.
    internal const string ServerAddress = "server.address";
    internal const string ServerPort = "server.port";

    // Error / exception attributes.
    internal const string ErrorType = "error.type";
    internal const string ExceptionMessage = "exception.message";

    // Constant values.
    internal const string MongoDbSystem = "mongodb";
}
