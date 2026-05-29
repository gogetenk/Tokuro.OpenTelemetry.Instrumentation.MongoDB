// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Testcontainers.MongoDb;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class EndToEndTests : IAsyncLifetime
{
    private const string ActivitySourceName = "Tokuro.OpenTelemetry.Instrumentation.MongoDB";
    private readonly MongoDbContainer _container = new MongoDbBuilder().Build();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception ex) when (ex is not SkipException)
        {
            throw new SkipException($"Docker / Mongo container unavailable: {ex.Message}");
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task EndToEnd_CapturesFindInsertUpdateActivities_WithExpectedTags()
    {
        var exported = new List<Activity>();
        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddMongoDBInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var settings = MongoClientSettings.FromConnectionString(_container.GetConnectionString());
        settings.AddOpenTelemetryInstrumentation();
        var client = new MongoClient(settings);
        var db = client.GetDatabase("itests");
        var collection = db.GetCollection<BsonDocument>("widgets");

        await collection.InsertOneAsync(new BsonDocument { { "name", "alpha" }, { "n", 1 } }, cancellationToken: CancellationToken.None);
        await collection.Find(new BsonDocument("name", "alpha")).FirstOrDefaultAsync(cancellationToken: CancellationToken.None);
        await collection.UpdateOneAsync(new BsonDocument("name", "alpha"), new BsonDocument("$set", new BsonDocument("n", 2)), cancellationToken: CancellationToken.None);

        tracer.ForceFlush();

        exported.Should().Contain(a => a.DisplayName.Contains("insert", StringComparison.OrdinalIgnoreCase));
        exported.Should().Contain(a => a.DisplayName.Contains("find", StringComparison.OrdinalIgnoreCase));
        exported.Should().Contain(a => a.DisplayName.Contains("update", StringComparison.OrdinalIgnoreCase));
        var anyActivity = exported.First(a => a.Source.Name == ActivitySourceName);
        anyActivity.GetTagItem(SemConv.DbSystemName).Should().Be("mongodb");
    }

    [Fact]
    public async Task EndToEnd_FailedOperation_DoesNotEmitExceptionMessage_OnlyErrorType()
    {
        // Arrange.
        // Note: duplicate-key writes return a successful CommandSucceededEvent with a writeErrors
        // array in the reply (the driver only then throws MongoWriteException to the caller), so
        // they never trigger a CommandFailedEvent. To exercise the failure-path tagging we send a
        // command the server itself rejects at the wire-protocol level; an unknown command name
        // reliably produces a CommandFailedEvent on every supported Mongo version.
        var exported = new List<Activity>();
        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddMongoDBInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var settings = MongoClientSettings.FromConnectionString(_container.GetConnectionString());
        settings.AddOpenTelemetryInstrumentation(new MongoDBInstrumentationOptions { SuppressExceptionMessage = true });
        var client = new MongoClient(settings);
        var db = client.GetDatabase("itests");

        // Act.
        var act = async () => await db.RunCommandAsync<BsonDocument>(
            new BsonDocument("notARealCommand", 1),
            cancellationToken: CancellationToken.None);
        await act.Should().ThrowAsync<MongoCommandException>();

        tracer.ForceFlush();

        // Assert.
        var failed = exported.Where(a => a.Status == ActivityStatusCode.Error).ToList();
        failed.Should().NotBeEmpty();
        failed[0].Tags.Should().NotContain(t => t.Key == "exception.message");
        failed[0].Tags.Should().Contain(t => t.Key == SemConv.ErrorType);
        failed[0].Status.Should().Be(ActivityStatusCode.Error);
    }

    private sealed class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}
