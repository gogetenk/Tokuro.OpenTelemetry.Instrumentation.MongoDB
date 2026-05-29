// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

// Produces a PII-safe JSON view of a MongoDB command: every scalar value becomes "?". Only
// the top-level collection-name string under a known command key is kept verbatim — it
// carries no user data and is useful for span naming.
internal sealed class MongoCommandRedactor
{
    internal const string TruncationMarker = "...[truncated]";

    // BSON depth is unbounded on the wire (only the 16 MB size is capped), so guard against
    // stack overflow on hostile input independently of the length cap.
    private const int MaxDepth = 100;

    private const int MaxCollectionNamePassThrough = 256;

    private static readonly HashSet<string> _collectionCommandKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "aggregate",
        "count",
        "create",
        "createIndexes",
        "delete",
        "distinct",
        "drop",
        "dropIndexes",
        "find",
        "findAndModify",
        "insert",
        "mapReduce",
        "update",
    };

    private static readonly JsonWriterSettings _jsonSettings = new()
    {
        OutputMode = JsonOutputMode.RelaxedExtendedJson,
    };

    private readonly int _maxLength;

    public MongoCommandRedactor(int maxLength)
    {
        // Floor so a truncated result keeps some content past the marker.
        _maxLength = Math.Max(maxLength, TruncationMarker.Length + 16);
    }

    public string Redact(BsonDocument command)
    {
        var redacted = RedactDocument(command, 0);
        var json = redacted.ToJson(_jsonSettings);

        if (json.Length <= _maxLength)
        {
            return json;
        }

        // Back off one char if the cut would split a surrogate pair.
        var sliceLength = _maxLength;
        if (char.IsHighSurrogate(json[sliceLength - 1]))
        {
            sliceLength -= 1;
        }

        return string.Concat(json.AsSpan(0, sliceLength), TruncationMarker);
    }

    private static BsonDocument RedactDocument(BsonDocument document, int depth)
    {
        if (depth > MaxDepth)
        {
            return new BsonDocument();
        }

        var result = new BsonDocument();

        foreach (var element in document.Elements)
        {
            if (depth == 0 && _collectionCommandKeys.Contains(element.Name) && element.Value.IsString)
            {
                var name = element.Value.AsString;
                if (name.Length > MaxCollectionNamePassThrough)
                {
                    name = string.Concat(name.AsSpan(0, MaxCollectionNamePassThrough), TruncationMarker);
                }

                result.Add(element.Name, new BsonString(name));
                continue;
            }

            result.Add(element.Name, RedactValue(element.Value, depth + 1));
        }

        return result;
    }

    private static BsonValue RedactValue(BsonValue value, int depth) =>
        depth > MaxDepth
            ? new BsonString("?")
            : value.BsonType switch
        {
            BsonType.Document => RedactDocument(value.AsBsonDocument, depth),
            BsonType.Array => new BsonArray(value.AsBsonArray.Select(item => RedactValue(item, depth + 1))),
            BsonType.Null => BsonNull.Value,
            _ => new BsonString("?"),
        };
}
