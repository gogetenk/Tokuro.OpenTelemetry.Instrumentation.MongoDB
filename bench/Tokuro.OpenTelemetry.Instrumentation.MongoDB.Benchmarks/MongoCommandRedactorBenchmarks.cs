// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MongoDB.Bson;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class MongoCommandRedactorBenchmarks
{
    private const int DefaultMaxLength = 4000;

    private MongoCommandRedactor _redactor = null!;
    private BsonDocument _smallFind = null!;
    private BsonDocument _deepNested = null!;
    private BsonDocument _largeInsert = null!;
    private BsonDocument _bulkWrite = null!;
    private BsonDocument _nearTruncationLimit = null!;

    [GlobalSetup]
    public void Setup()
    {
        _redactor = new MongoCommandRedactor(DefaultMaxLength);

        _smallFind = BsonDocument.Parse("""
        {
          "find": "users",
          "filter": { "tenantId": "acme", "status": "active", "email": "ada@example.com" },
          "limit": 1,
          "$db": "app"
        }
        """);

        _deepNested = BsonDocument.Parse("""
        {
          "aggregate": "orders",
          "pipeline": [
            { "$match": { "status": "paid", "createdAt": { "$gte": { "$date": "2025-01-01T00:00:00Z" } } } },
            { "$lookup": { "from": "customers", "localField": "customerId", "foreignField": "_id", "as": "customer" } },
            { "$group": { "_id": "$customer.country", "total": { "$sum": "$amount" }, "count": { "$sum": 1 } } },
            { "$project": { "country": "$_id", "total": 1, "count": 1, "_id": 0 } }
          ],
          "cursor": { "batchSize": 100 },
          "$db": "app"
        }
        """);

        var largeFields = new BsonDocument { { "_id", "user-42" } };
        for (var i = 0; i < 49; i++)
        {
            largeFields.Add($"field_{i:00}", $"value-{i}-{Guid.NewGuid():n}");
        }

        _largeInsert = new BsonDocument
        {
            { "insert", "users" },
            { "documents", new BsonArray { largeFields } },
            { "ordered", true },
            { "$db", "app" },
        };

        var bulkDocs = new BsonArray();
        for (var d = 0; d < 50; d++)
        {
            var doc = new BsonDocument { { "_id", $"id-{d:000}" } };
            for (var i = 0; i < 19; i++)
            {
                doc.Add($"k{i:00}", $"v-{d}-{i}");
            }
            bulkDocs.Add(doc);
        }

        _bulkWrite = new BsonDocument
        {
            { "insert", "events" },
            { "documents", bulkDocs },
            { "ordered", false },
            { "$db", "app" },
        };

        var bigArray = new BsonArray();
        for (var i = 0; i < 200; i++)
        {
            bigArray.Add(new BsonDocument
            {
                { "k", $"key_{i:000}" },
                { "v", new string('x', 32) },
            });
        }

        _nearTruncationLimit = new BsonDocument
        {
            { "update", "events" },
            { "updates", new BsonArray
                {
                    new BsonDocument
                    {
                        { "q", new BsonDocument { { "tenantId", "acme" } } },
                        { "u", new BsonDocument { { "$set", new BsonDocument { { "payload", bigArray } } } } },
                        { "multi", true },
                    },
                }
            },
            { "$db", "app" },
        };
    }

    [Benchmark(Baseline = true)]
    public string Redact_SmallFind() => _redactor.Redact(_smallFind);

    [Benchmark]
    public string Redact_DeepNested() => _redactor.Redact(_deepNested);

    [Benchmark]
    public string Redact_LargeInsert() => _redactor.Redact(_largeInsert);

    [Benchmark]
    public string Redact_BulkWrite() => _redactor.Redact(_bulkWrite);

    [Benchmark]
    public string Redact_NearTruncationLimit() => _redactor.Redact(_nearTruncationLimit);
}
