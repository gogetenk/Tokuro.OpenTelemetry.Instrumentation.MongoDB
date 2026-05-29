# Security Policy

## Supported versions

Security fixes are issued for the **two most recent minor releases**. Older releases receive fixes only for vulnerabilities rated Critical (CVSS >= 9.0).

| Version      | Supported |
| ------------ | --------- |
| `Unreleased` | Yes       |
| `0.1.x`      | Yes (upcoming first release) |

The table is updated with each minor release.

## Reporting a vulnerability

**Do not file public issues for security reports.**

Use GitHub **private security advisories** on this repository:

<https://github.com/Tokuro/Tokuro.OpenTelemetry.Instrumentation.MongoDB/security/advisories/new>

Include:

- Affected version(s)
- A repro or a precise enough description that one can be written
- Impact assessment (what attacker, what capability, what data)
- Suggested remediation if you have one

## Response process

| Step             | Target          |
| ---------------- | --------------- |
| Acknowledgement  | within 7 days   |
| Initial assessment & severity | within 14 days |
| Fix released or coordinated disclosure | within 30 days of acknowledgement |

If the fix requires upstream changes (MongoDB driver, OpenTelemetry SDK), the 30-day window is paused while coordinating with that project and the reporter is notified.

## Credit

Reporters are credited in the GitHub Security Advisory and in `CHANGELOG.md` under the `Security` bucket of the fixing release, unless they request anonymity.

## Out of scope

- Vulnerabilities in `MongoDB.Driver` itself — report to the [MongoDB driver project](https://jira.mongodb.org/).
- Vulnerabilities in the OpenTelemetry SDK or Exporter packages — report to the [open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet) project.
- Consumer misconfiguration that disables redaction or downgrades the defaults — by design these are opt-in choices and the resulting telemetry payload is the consumer's responsibility.
