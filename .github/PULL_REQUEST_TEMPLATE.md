# Summary

<!-- One paragraph: what changes, and why. Link the issue this resolves (`Fixes #123`). -->

## Type of change

- [ ] Bug fix (non-breaking)
- [ ] Feature (non-breaking)
- [ ] Performance change (no behavior change)
- [ ] Breaking change
- [ ] Documentation
- [ ] Build / CI / chore

## Checklist

- [ ] Tests added or updated; `dotnet test --filter "Category!=Integration"` passes locally
- [ ] If the hot path was touched: BenchmarkDotNet run attached below (before / after, mean + alloc/op)
- [ ] Public API change documented in XML doc comments and in README/docs as relevant
- [ ] `CHANGELOG.md` `[Unreleased]` updated under the correct bucket
- [ ] Sample project updated if it demonstrates the changed surface
- [ ] All commit titles use Conventional Commits
- [ ] All commits are DCO-signed (`git commit -s`)

## Benchmarks (if hot path touched)

<details>
<summary>Before</summary>

```text
<!-- paste BenchmarkDotNet table -->
```

</details>

<details>
<summary>After</summary>

```text
<!-- paste BenchmarkDotNet table -->
```

</details>

## Reviewer notes

<!-- Anything that helps reviewers: design alternatives considered, areas you want extra eyes on, follow-up work intentionally deferred. -->
