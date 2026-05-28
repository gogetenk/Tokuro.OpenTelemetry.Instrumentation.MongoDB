// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MongoDB.Bson;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests;

public sealed class MongoCommandRedactorTests
{
    private const string TruncationMarker = "...[truncated]";
    private const int DefaultMaxLength = 4_000;
    private const string Placeholder = "\"?\"";

    [Fact]
    public void Redact_ShouldKeepCommandShapeAndCollectionNameOnly()
    {
        var command = new BsonDocument
        {
            { "find", "users" },
            { "filter", new BsonDocument
                {
                    { "email", "john.doe@example.com" },
                    { "age", new BsonDocument("$gt", 42) }
                }
            },
            { "limit", 10 }
        };
        var redactor = new MongoCommandRedactor(DefaultMaxLength);

        var result = redactor.Redact(command);

        result.Should().Contain("\"find\" : \"users\"");
        result.Should().Contain("\"email\" : \"?\"");
        result.Should().Contain("\"$gt\" : \"?\"");
        result.Should().Contain("\"limit\" : \"?\"");
        result.Should().NotContain("john.doe@example.com");
        result.Should().NotContain("42");
        result.Should().NotContain("10");
    }

    [Fact]
    public void Redact_ShouldRedactNestedArrayValues()
    {
        var command = new BsonDocument
        {
            { "aggregate", "orders" },
            { "pipeline", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("accountId", "A-123")),
                    new BsonDocument("$limit", 25)
                }
            }
        };
        var redactor = new MongoCommandRedactor(DefaultMaxLength);

        var result = redactor.Redact(command);

        result.Should().Contain("\"aggregate\" : \"orders\"");
        result.Should().Contain("\"accountId\" : \"?\"");
        result.Should().Contain("\"$limit\" : \"?\"");
        result.Should().NotContain("A-123");
        result.Should().NotContain("25");
    }

    [Fact]
    public void Redact_ShouldTruncateLongCommandText()
    {
        const int maxLength = 20;
        var command = new BsonDocument
        {
            { "find", "users" },
            { "filter", new BsonDocument("field", "value") }
        };
        var redactor = new MongoCommandRedactor(maxLength);

        var result = redactor.Redact(command);

        result.Should().EndWith(TruncationMarker);
        result.Length.Should().Be(maxLength + TruncationMarker.Length);
    }

    [Theory]
    [InlineData("find", "users")]
    [InlineData("aggregate", "orders")]
    [InlineData("insert", "products")]
    [InlineData("update", "carts")]
    [InlineData("delete", "sessions")]
    [InlineData("count", "events")]
    [InlineData("distinct", "tags")]
    [InlineData("mapReduce", "logs")]
    [InlineData("findAndModify", "queue")]
    public void Redact_PreservesCollectionName_OnAllKnownCommands(string commandName, string collectionName)
    {
        var command = new BsonDocument
        {
            { commandName, collectionName },
            { "filter", new BsonDocument("x", 1) }
        };
        var redactor = new MongoCommandRedactor(DefaultMaxLength);

        var result = redactor.Redact(command);

        result.Should().Contain($"\"{commandName}\" : \"{collectionName}\"");
    }

    [Fact]
    public void Redact_ReplacesNestedFilterValues_WithPlaceholder()
    {
        var command = new BsonDocument
        {
            { "find", "users" },
            { "filter", new BsonDocument
                {
                    { "profile", new BsonDocument
                        {
                            { "address", new BsonDocument
                                {
                                    { "city", "Paris" },
                                    { "zip", "75001" }
                                }
                            }
                        }
                    }
                }
            }
        };
        var redactor = new MongoCommandRedactor(DefaultMaxLength);

        var result = redactor.Redact(command);

        result.Should().Contain("\"city\" : \"?\"");
        result.Should().Contain("\"zip\" : \"?\"");
        result.Should().NotContain("Paris");
        result.Should().NotContain("75001");
    }

    [Fact]
    public void Redact_TruncatesAtMaxLength_WhenCommandIsLarge()
    {
        const int maxLength = 50;
        var filter = new BsonDocument();
        for (var i = 0; i < 100; i++)
        {
            filter.Add($"field_{i}", $"value_{i}");
        }
        var command = new BsonDocument
        {
            { "find", "users" },
            { "filter", filter }
        };
        var redactor = new MongoCommandRedactor(maxLength);

        var result = redactor.Redact(command);

        result.Should().EndWith(TruncationMarker);
        result.Length.Should().Be(maxLength + TruncationMarker.Length);
    }

    [Fact]
    public void Redact_HandlesEmptyCommand()
    {
        var command = new BsonDocument();
        var redactor = new MongoCommandRedactor(DefaultMaxLength);

        var result = redactor.Redact(command);

        result.Should().NotBeNull();
        result.Should().NotContain(TruncationMarker);
    }

    [Fact]
    public void Redact_PreservesArrayStructure_RedactsScalarElements()
    {
        var command = new BsonDocument
        {
            { "find", "users" },
            { "filter", new BsonDocument
                {
                    { "tags", new BsonArray { "vip", "newsletter", "beta" } }
                }
            }
        };
        var redactor = new MongoCommandRedactor(DefaultMaxLength);

        var result = redactor.Redact(command);

        result.Should().Contain("[");
        result.Should().Contain("]");
        result.Should().Contain(Placeholder);
        result.Should().NotContain("vip");
        result.Should().NotContain("newsletter");
        result.Should().NotContain("beta");
    }
}
