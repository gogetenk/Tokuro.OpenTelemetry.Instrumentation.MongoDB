// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;

// OpenTelemetry attribute and event names used by this instrumentation, split between the
// legacy and the current stable database semantic conventions.
internal static class SemConv
{
    // Legacy database attributes.
    internal const string DbSystem = "db.system";
    internal const string DbStatement = "db.statement";
    internal const string DbName = "db.name";
    internal const string DbOperation = "db.operation";

    // Stable database attributes.
    internal const string DbSystemName = "db.system.name";
    internal const string DbQueryText = "db.query.text";
    internal const string DbNamespace = "db.namespace";
    internal const string DbOperationName = "db.operation.name";
    internal const string DbCollectionName = "db.collection.name";

    internal const string ServerAddress = "server.address";
    internal const string ServerPort = "server.port";

    internal const string ErrorType = "error.type";

    // Exception detail belongs on a span event named "exception", not on span attributes.
    internal const string ExceptionEventName = "exception";
    internal const string ExceptionType = "exception.type";
    internal const string ExceptionMessage = "exception.message";

    internal const string MongoDbSystem = "mongodb";
}
