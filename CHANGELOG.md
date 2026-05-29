# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `AddOpenTelemetryInstrumentation(this MongoClientSettings, Action<MongoDBInstrumentationOptions>)` callback overload, matching the documented quick-start usage.

### Changed

- README: local-first onboarding — the quick-start now uses the Console exporter so spans are visible on a first run, lists the required companion packages (`OpenTelemetry.Extensions.Hosting` + an exporter), and adds a "verify locally / flush before exit" note. (Docs only; no code change.)
- Failed commands now record the exception message (when `SuppressExceptionMessage = false`) on an OTel `exception` span event carrying `exception.type` + `exception.message`, instead of an off-spec `exception.message` span attribute. `error.type` remains a span attribute.
- Replaced the `FluentAssertions` test dependency with `AwesomeAssertions` (MIT) — FluentAssertions 8+ moved to a paid commercial license. Test-only change; no impact on the shipped package.

### Removed

- Unused `MongoCommandRedactor.TruncationMarkerLength` member.

### Fixed

- Documentation corrected to match the shipped behavior: in-flight overflow drops the new activity (it does not clear the whole map); `AddMongoDBInstrumentation` takes no options argument; `span.type` is not emitted; `db.operation.name` is gated on `EmitStableAttributes`.

### Security

- Documented that redaction preserves field/key names (only scalar values are replaced), so PII stored as a document key can surface in spans; mitigations noted in `SECURITY.md`.

## [0.1.0] - 2026-05-29

### Added

- Public API `AddMongoDBInstrumentation(this TracerProviderBuilder)` — subscribes the tracer provider to the instrumentation's `ActivitySource`. Options are configured on the `MongoClientSettings` side.
- Public API `AddOpenTelemetryInstrumentation(this MongoClientSettings, MongoDBInstrumentationOptions? options = null)` for manual `MongoClientSettings` wiring.
- BSON tree-walk redaction enabled by default; scalars are replaced with `"?"`, collection names and operators preserved.
- `exception.message` suppression on failed commands (driver exceptions echo BSON fragments).
- Dual-write OTel DB semantic-convention attributes: legacy (`db.system`, `db.statement`, `db.name`) and stable (`db.system.name`, `db.query.text`, `db.namespace`).
- Bounded in-flight `Activity` map (`MaxInFlightCommands`, default 10 000) keyed by a struct connection-id/request-id wrapper to avoid boxing. On overflow the new command's activity is dropped (stopped, not tracked) while existing entries are preserved, and an `EventSource` counter is emitted.
- ActivitySource `"Tokuro.OpenTelemetry.Instrumentation.MongoDB"`.
- Multi-target build for `net8.0`, `net9.0`, `net10.0`.

[Unreleased]: https://github.com/gogetenk/Tokuro.OpenTelemetry.Instrumentation.MongoDB/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/gogetenk/Tokuro.OpenTelemetry.Instrumentation.MongoDB/releases/tag/v0.1.0
