# Migrating from `MongoDB.Driver.Core.Extensions.DiagnosticSources`

This library is a near-drop-in replacement for Jimmy Bogard's [`MongoDB.Driver.Core.Extensions.DiagnosticSources`](https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources). The wiring is different, the public API is intentionally close, and the defaults are stricter.

## Side-by-side: registration

### Before (`jbogard`)

```csharp
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

var settings = MongoClientSettings.FromConnectionString(cs);
var options  = new InstrumentationOptions { CaptureCommandText = true };
settings.ClusterConfigurator = cb =>
    cb.Subscribe(new DiagnosticsActivityEventSubscriber(options));

var client = new MongoClient(settings);

// In OTel pipeline:
.WithTracing(t => t
    .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
    .AddOtlpExporter());
```

### After (this library, manual settings)

```csharp
using Tokuro.OpenTelemetry.Instrumentation.MongoDB;

var settings = MongoClientSettings.FromConnectionString(cs);
settings.AddOpenTelemetryInstrumentation(new MongoDBInstrumentationOptions
{
    CaptureCommandText = true, // redacted by default — see options table
});
var client = new MongoClient(settings);

// In OTel pipeline:
.WithTracing(t => t
    .AddSource("Tokuro.OpenTelemetry.Instrumentation.MongoDB")
    .AddOtlpExporter());
```

### After (this library, DI-first — recommended)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddMongoDBInstrumentation()
        .AddOtlpExporter());
```

Remember to update the `ActivitySource` name in any custom processor, sampler, or filter that referenced the old source.

## Option mapping

| `jbogard` option         | This library             | Behavior change                                                                                       |
| ------------------------ | ------------------------ | ----------------------------------------------------------------------------------------------------- |
| `CaptureCommandText`     | `CaptureCommandText`     | **Now safe-by-default.** When `true`, BSON is walked and scalars replaced with `"?"`. Raw capture is a separate explicit opt-in. |
| `ShouldStartActivity`    | `FilterCommand`          | Same predicate semantics, different parameter type (`CommandStartedEvent` directly).                  |
| n/a                      | `SuppressExceptionMessage` | **New.** Drops `exception.message` on failed commands to prevent BSON-fragment leakage. Default `true`. |
| n/a                      | `MaxInFlightCommands`    | **New.** Bounds the in-flight Activity map. Default `10_000`. Upstream is unbounded.                   |
| n/a                      | `EmitLegacyAttributes`   | **New.** Controls the legacy DB sem-conv attribute set. Default `true`.                                |
| n/a                      | `EmitStableAttributes`   | **New.** Controls the stable DB sem-conv attribute set (`db.system.name`, etc.). Default `true`.       |
| n/a                      | `MaxCommandTextLength`   | **New.** Truncates redacted command text. Default `4000`.                                              |

## What you gain

- BSON redaction by default; raw capture is an explicit choice rather than the default behavior.
- `exception.message` suppression on failed commands.
- Stable OTel DB semantic-convention attributes alongside the legacy ones, so dashboards built against either set keep working through the migration window.
- A bounded in-flight Activity map — a leaky cursor cannot grow the heap forever.

## What you keep

- The conceptual model: one `ActivitySource`, one `Activity` per command, `CommandStartedEvent` → `CommandSucceededEvent` / `CommandFailedEvent`.
- Compatibility with any OTel exporter and any sampler.
- Zero-overhead sampled-out path gated on `Activity.IsAllDataRequested`.
