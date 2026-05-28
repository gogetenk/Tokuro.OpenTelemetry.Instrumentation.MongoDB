# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.1.0] - YYYY-MM-DD

### Added

- Public API `AddMongoDBInstrumentation(this TracerProviderBuilder, Action<MongoDBInstrumentationOptions>?)`.
- Public API `AddOpenTelemetryInstrumentation(this MongoClientSettings, MongoDBInstrumentationOptions? options = null)` for manual `MongoClientSettings` wiring.
- BSON tree-walk redaction enabled by default; scalars are replaced with `"?"`, collection names and operators preserved.
- `exception.message` suppression on failed commands (driver exceptions echo BSON fragments).
- Dual-write OTel DB semantic-convention attributes: legacy (`db.system`, `db.statement`, `db.name`) and stable (`db.system.name`, `db.query.text`, `db.namespace`).
- Bounded in-flight `Activity` map (`MaxInFlightCommands`, default 10 000) keyed by a struct connection-id/request-id wrapper to avoid boxing. On overflow, all in-flight entries are stopped and the map is cleared (defensive cap, not LRU).
- ActivitySource `"Tokuro.OpenTelemetry.Instrumentation.MongoDB"`.
- Multi-target build for `net8.0`, `net9.0`, `net10.0`.

[Unreleased]: https://github.com/<OWNER>/Tokuro.OpenTelemetry.Instrumentation.MongoDB/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/<OWNER>/Tokuro.OpenTelemetry.Instrumentation.MongoDB/releases/tag/v0.1.0
