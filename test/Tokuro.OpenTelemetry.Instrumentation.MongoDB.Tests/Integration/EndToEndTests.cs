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
        var exported = new List<Activity>();
        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddMongoDBInstrumentation(opts => opts.SuppressExceptionMessage = true)
            .AddInMemoryExporter(exported)
            .Build();

        var settings = MongoClientSettings.FromConnectionString(_container.GetConnectionString());
        settings.AddOpenTelemetryInstrumentation();
        var client = new MongoClient(settings);
        var db = client.GetDatabase("itests");
        var collection = db.GetCollection<BsonDocument>("uniques");

        var indexModel = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("k"),
            new CreateIndexOptions { Unique = true });
        await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: CancellationToken.None);
        await collection.InsertOneAsync(new BsonDocument("k", "dup"), cancellationToken: CancellationToken.None);

        var act = async () => await collection.InsertOneAsync(new BsonDocument("k", "dup"), cancellationToken: CancellationToken.None);
        await act.Should().ThrowAsync<MongoWriteException>();

        tracer.ForceFlush();

        var failed = exported.Where(a => a.Status == ActivityStatusCode.Error).ToList();
        failed.Should().NotBeEmpty();
        failed.Should().AllSatisfy(a =>
        {
            a.GetTagItem(SemConv.ErrorType).Should().NotBeNull();
            a.GetTagItem("exception.message").Should().BeNull();
        });
    }

    private sealed class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}
