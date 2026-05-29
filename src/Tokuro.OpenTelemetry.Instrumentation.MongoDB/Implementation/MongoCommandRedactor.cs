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
/// <c>aggregate</c>) are preserved (capped at <see cref="MaxCollectionNamePassThrough"/>
/// characters) because they carry no user data and are useful for span naming and grouping.
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

    /// <summary>
    /// Hard cap on the depth of BSON recursion. An adversarial document nested deeper than
    /// this is collapsed to <c>"?"</c> at the cutoff to prevent stack overflow. The MongoDB
    /// wire protocol bounds documents at 16 MB but does not bound depth, so this defense is
    /// independent of the size cap and must remain in place.
    /// </summary>
    private const int MaxDepth = 100;

    /// <summary>
    /// Maximum number of characters preserved verbatim from a top-level collection-name
    /// string. Collection names can technically be arbitrarily long; capping the pass-through
    /// avoids a single oversized identifier blowing past the overall command-text budget.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoCommandRedactor"/> class.
    /// </summary>
    /// <param name="maxLength">Maximum length, in characters, of the produced string.
    /// Floored to <c>TruncationMarker.Length + 16</c> so a truncated result always contains
    /// at least 16 characters of content plus the marker, and so the slice cannot land
    /// mid-surrogate after the surrogate-back-off step.</param>
    public MongoCommandRedactor(int maxLength)
    {
        _maxLength = Math.Max(maxLength, TruncationMarker.Length + 16);
    }

    /// <summary>
    /// Walks the provided BSON document, replacing every scalar value with the placeholder
    /// <c>"?"</c> (except top-level collection-name strings under known command keys, capped
    /// at <see cref="MaxCollectionNamePassThrough"/> characters), then serializes the result
    /// to relaxed extended JSON and truncates it to the configured maximum length. The slice
    /// position is adjusted backward by one if it would split a UTF-16 surrogate pair.
    /// </summary>
    /// <param name="command">The raw command document captured by the driver.</param>
    /// <returns>A PII-safe JSON string suitable for attaching to a span.</returns>
    public string Redact(BsonDocument command)
    {
        var redacted = RedactDocument(command, 0);
        var json = redacted.ToJson(_jsonSettings);

        if (json.Length <= _maxLength)
        {
            return json;
        }

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
            // Stop recursing on hostile input; the caller substitutes a scalar placeholder.
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
