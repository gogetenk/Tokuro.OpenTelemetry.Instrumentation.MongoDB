// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;

/// <summary>
/// BSON tree walker that produces a PII-safe textual representation of a MongoDB command
/// by replacing every scalar value with the placeholder <c>"?"</c>. Top-level string values
/// associated with a well-known collection-name command key (e.g. <c>find</c>, <c>insert</c>,
/// <c>aggregate</c>) are preserved because they carry no user data and are useful for
/// span naming and grouping.
/// </summary>
internal sealed class MongoCommandRedactor
{
    /// <summary>
    /// Constant marker appended to the redacted command text when it exceeds the configured
    /// maximum length. Exposed so callers can size buffers without duplicating the literal.
    /// </summary>
    internal const string TruncationMarker = "...[truncated]";

    /// <summary>
    /// Length of <see cref="TruncationMarker"/> in characters. Derived from the marker
    /// itself so the two cannot drift apart.
    /// </summary>
    internal static readonly int TruncationMarkerLength = TruncationMarker.Length;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoCommandRedactor"/> class.
    /// </summary>
    /// <param name="maxLength">Maximum length, in characters, of the produced string.
    /// Non-positive values fall back to <c>4_000</c>.</param>
    public MongoCommandRedactor(int maxLength)
    {
        _maxLength = maxLength > 0 ? maxLength : 4_000;
    }

    /// <summary>
    /// Walks the provided BSON document, replacing every scalar value with the placeholder
    /// <c>"?"</c> (except top-level collection-name strings under known command keys), then
    /// serializes the result to relaxed extended JSON and truncates it to the configured
    /// maximum length.
    /// </summary>
    /// <param name="command">The raw command document captured by the driver.</param>
    /// <returns>A PII-safe JSON string suitable for attaching to a span.</returns>
    public string Redact(BsonDocument command)
    {
        var redacted = RedactDocument(command, 0);
        var json = redacted.ToJson(_jsonSettings);

        return json.Length <= _maxLength
            ? json
            : string.Concat(json.AsSpan(0, _maxLength), TruncationMarker);
    }

    private static BsonDocument RedactDocument(BsonDocument document, int depth)
    {
        var result = new BsonDocument();

        foreach (var element in document.Elements)
        {
            if (depth == 0 && _collectionCommandKeys.Contains(element.Name) && element.Value.IsString)
            {
                result.Add(element.Name, element.Value);
                continue;
            }

            result.Add(element.Name, RedactValue(element.Value, depth + 1));
        }

        return result;
    }

    private static BsonValue RedactValue(BsonValue value, int depth)
        => value.BsonType switch
        {
            BsonType.Document => RedactDocument(value.AsBsonDocument, depth),
            BsonType.Array => new BsonArray(value.AsBsonArray.Select(item => RedactValue(item, depth + 1))),
            BsonType.Null => BsonNull.Value,
            _ => new BsonString("?"),
        };
}
