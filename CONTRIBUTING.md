# Contributing

Thanks for your interest. This document is the contract between contributors and maintainers.

## Issue triage

- **Bug reports** must include a minimal repro (preferably a failing test) or a precise enough span dump that one can be written. Reports without either are closed as `needs-repro`.
- **Feature requests** must reference the OTel DB semantic-convention section they relate to, or articulate why the feature is in scope for an instrumentation library rather than a consumer-side concern.
- **Questions** belong in GitHub Discussions, not Issues.
- **Security reports** belong in GitHub private security advisories — see [`SECURITY.md`](./SECURITY.md). Never in public Issues.

Labels we use: `bug`, `enhancement`, `perf`, `docs`, `chore`, `needs-repro`, `needs-design`, `good-first-issue`, `help-wanted`.

## Branch model

`main` is always releasable. Feature work happens on short-lived branches off `main`, using these prefixes:

- `feat/<slug>` — new public API or new behavior
- `fix/<slug>` — bug fix, no API change
- `perf/<slug>` — performance change, no behavior change
- `docs/<slug>` — documentation only
- `chore/<slug>` — build, CI, refactor with no observable change

## Conventional Commits

Commit titles follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```
feat(redaction): emit collection name unredacted for aggregate stages
fix(map): drop oldest entry on overflow instead of newest
perf(hot-path): elide allocation when IsAllDataRequested is false
docs(readme): clarify backend interop section
chore(ci): bump actions/checkout to v4
```

The body — when present — explains the *why*. The diff explains the *what*.

## DCO sign-off

Every commit must be signed off per the [Developer Certificate of Origin](https://developercertificate.org/):

```bash
git commit -s -m "feat(...): ..."
```

PRs with unsigned commits will not be merged. There is no CLA.

## PR checklist

Before requesting review:

- [ ] Tests added or updated; unit tests pass locally
- [ ] If the hot path was touched: BenchmarkDotNet run attached to the PR description (before / after, mean + alloc/op)
- [ ] Public API change documented in XML doc comments and in the README/docs if user-visible
- [ ] `CHANGELOG.md` `[Unreleased]` section updated under the right bucket
- [ ] Sample project updated if it demonstrates the changed surface
- [ ] All commit titles are Conventional Commits
- [ ] All commits are DCO-signed (`-s`)

## Dev environment

- **.NET 8 SDK** minimum to build; SDK 9 and 10 are required to run the full TFM matrix locally. CI provides all three.
- **Docker** required for integration tests (Testcontainers spins up a MongoDB instance per fixture).
- An editor with EditorConfig and Roslyn analyzers — Rider or VS Code with the C# Dev Kit.

## Running tests

Unit-only (no Docker required):

```bash
dotnet test --filter "Category!=Integration"
```

Full suite (Docker required):

```bash
dotnet test
```

## Running benchmarks

```bash
dotnet run -c Release --project bench/Tokuro.OpenTelemetry.Instrumentation.MongoDB.Benchmarks -- --filter '*'
```

BenchmarkDotNet results are written to `BenchmarkDotNet.Artifacts/`. Attach the relevant markdown report to the PR.

## Releasing

Maintainers only. Releases are tag-driven:

1. Land all PRs targeting the release on `main`.
2. Update `CHANGELOG.md`: move `[Unreleased]` content under a new `[X.Y.Z] - YYYY-MM-DD` heading.
3. Commit, push, tag `vX.Y.Z`, push tag.
4. The `release.yml` workflow builds, packs, pushes to nuget.org, and creates the GitHub Release.
