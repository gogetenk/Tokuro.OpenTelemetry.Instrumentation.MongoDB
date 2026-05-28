# Sample.AspNetCore

A minimal ASP.NET Core app that demonstrates `Tokuro.OpenTelemetry.Instrumentation.MongoDB` end-to-end with the OpenTelemetry **console exporter** — no external observability backend required.

## How to run

1. Start a local MongoDB:

   ```bash
   docker run -d --name mongo -p 27017:27017 mongo:7
   ```

2. From this directory:

   ```bash
   dotnet run
   ```

3. Hit the endpoints (the app listens on `http://localhost:5080`):

   ```bash
   # Create a user
   curl -X POST http://localhost:5080/users \
        -H 'content-type: application/json' \
        -d '{"name":"Ada Lovelace","email":"ada@example.com"}'
   # → { "id": "65f2c0a1e3a4b5c6d7e8f9a0", "name": "Ada Lovelace", "email": "ada@example.com" }

   # Fetch the user by id (copy the id from the previous response)
   curl http://localhost:5080/users/65f2c0a1e3a4b5c6d7e8f9a0

   # List all users
   curl http://localhost:5080/users
   ```

## What you'll see

For every Mongo round-trip, an `Activity` is written to **stdout** by the console exporter with the OTel DB semantic-convention tags. Values are redacted by default — every scalar in the command document is replaced with `?`:

```
Activity.TraceId:       6f9a3a8e1c5d4b7a8e2f1c3d4b5a6c7d
Activity.SpanId:        a1b2c3d4e5f60718
Activity.DisplayName:   MongoDB find users
Activity.Kind:          Client
Activity.StartTime:     2026-05-28T10:15:42.1234567Z
Activity.Duration:      00:00:00.0034210
Activity.Tags:
    span.type:            mongodb
    db.operation.name:    find
    db.system:            mongodb
    db.name:              sample
    db.statement:         { "find" : "users", "filter" : { "_id" : "?" }, "$db" : "?" }
    db.system.name:       mongodb
    db.namespace:         sample
    db.query.text:        { "find" : "users", "filter" : { "_id" : "?" }, "$db" : "?" }
    db.collection.name:   users
    server.address:       localhost
    server.port:          27017
StatusCode:             Ok
```

Notice `db.statement` / `db.query.text`: every scalar that came from the request body (`_id` value, `$db` value) is `?`. The collection name (`users`) and the operator keys are preserved.

## Where to look

- **Redacted command text**: the `db.statement` (legacy) and `db.query.text` (stable) tags carry the same redacted JSON.
- **Failures**: trigger one by `GET /users/not-a-valid-objectid` — the span status flips to `Error` and `error.type` is set. By default `exception.message` is suppressed (driver messages can echo BSON fragments).

## Notes

- The instrumentation is attached two ways and both are required:
  - `MongoClientSettings.AddOpenTelemetryInstrumentation()` hooks the driver's command events on the specific client.
  - `TracerProviderBuilder.AddMongoDBInstrumentation()` subscribes the `ActivitySource` so spans are exported.
- See the [root README](../../README.md) for the full options surface (redaction, statement length cap, in-flight bound, etc.).
