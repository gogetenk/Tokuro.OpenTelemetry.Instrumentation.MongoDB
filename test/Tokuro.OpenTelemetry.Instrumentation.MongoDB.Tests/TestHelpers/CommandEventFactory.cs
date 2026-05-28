// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Servers;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests.TestHelpers;

internal static class CommandEventFactory
{
    public static ConnectionId NewConnectionId(EndPoint? endpoint = null)
    {
        endpoint ??= new DnsEndPoint("localhost", 27017);
        var serverId = new ServerId(new ClusterId(1), endpoint);
        return new ConnectionId(serverId, 1);
    }

    public static CommandStartedEvent Started(
        string commandName = "find",
        string collection = "users",
        string database = "testdb",
        BsonDocument? command = null,
        EndPoint? endpoint = null,
        int requestId = 1)
    {
        command ??= new BsonDocument
        {
            { commandName, collection },
            { "$db", database },
            { "filter", new BsonDocument("x", 1) }
        };
        return new CommandStartedEvent(
            commandName,
            command,
            new DatabaseNamespace(database),
            operationId: requestId,
            requestId: requestId,
            connectionId: NewConnectionId(endpoint));
    }

    public static CommandSucceededEvent Succeeded(
        string commandName = "find",
        int requestId = 1,
        EndPoint? endpoint = null)
    {
        return new CommandSucceededEvent(
            commandName,
            new BsonDocument("ok", 1),
            new DatabaseNamespace("testdb"),
            operationId: requestId,
            requestId: requestId,
            connectionId: NewConnectionId(endpoint),
            duration: TimeSpan.FromMilliseconds(5));
    }

    public static CommandFailedEvent Failed(
        string commandName = "find",
        int requestId = 1,
        Exception? exception = null,
        EndPoint? endpoint = null)
    {
        exception ??= new InvalidOperationException("boom — secret value 12345");
        return new CommandFailedEvent(
            commandName,
            new DatabaseNamespace("testdb"),
            exception,
            operationId: requestId,
            requestId: requestId,
            connectionId: NewConnectionId(endpoint),
            duration: TimeSpan.FromMilliseconds(5));
    }
}
