// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MongoDB.Bson;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB;

var builder = WebApplication.CreateBuilder(args);

var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo")
    ?? "mongodb://localhost:27017";
var databaseName = builder.Configuration["Mongo:Database"] ?? "sample";
var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "Sample.AspNetCore";

builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);
    settings.AddOpenTelemetryInstrumentation();
    return new MongoClient(settings);
});

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>("users"));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(t => t
        .AddMongoDBInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = serviceName, status = "ok" }));

app.MapGet("/users/{id}", async (string id, IMongoCollection<BsonDocument> users, CancellationToken ct) =>
{
    var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
    using var cursor = await users.FindAsync(filter, cancellationToken: ct);
    var doc = await cursor.FirstOrDefaultAsync(ct);
    return doc is null
        ? Results.NotFound(new { id })
        : Results.Ok(BsonTypeMapper.MapToDotNetValue(doc));
});

app.MapPost("/users", async (CreateUserRequest req, IMongoCollection<BsonDocument> users, CancellationToken ct) =>
{
    var id = req.Id ?? Guid.NewGuid().ToString("n");
    var doc = new BsonDocument
    {
        { "_id", id },
        { "name", req.Name },
        { "email", req.Email },
        { "createdAt", DateTime.UtcNow },
    };
    await users.InsertOneAsync(doc, cancellationToken: ct);
    return Results.Created($"/users/{id}", new { id, req.Name, req.Email });
});

app.Run();

internal sealed record CreateUserRequest(string? Id, string Name, string Email);
