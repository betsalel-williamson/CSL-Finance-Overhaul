# Agent instructions — Finance Overhaul

Trunk-based development on `main`. Short-lived `cursor/*-5acf` branches; squash-merge when gates are
green.

## Required before merge / “done”

```bash
npm run hooks:agent-validate
```

That runs layer lint, Market unit+perf tests, Mod host tests (no Cities), csharpier check, and
Merlon docs. Do **not** claim merge-ready without it.

## Test layers

| Command             | Layer                                    | Cities assemblies              |
| ------------------- | ---------------------------------------- | ------------------------------ |
| `npm run test:unit` | `FinanceOverhaul.Market` math/probe/perf | Never                          |
| `npm run test:mod`  | Mod host identity                        | Off (`EnableCitiesRefs=false`) |
| Local game loop     | Harmony + Stock Exchange                 | Required on your machine       |

Stock business logic and performance budgets live only under `src/FinanceOverhaul.Market/` and
`tests/FinanceOverhaul.Market.Tests/`. Cities Harmony / `ICities` stay in `mod/Host/`.

## Git hooks

- **pre-commit** — lint-staged (prettier + csharpier)
- **commit-msg** — commitlint conventional commits
- **pre-push** — layer lint + `test:unit` + docs; on `main` also `test:mod` + format:check

## Docs

Edit Merlon shards under `docs/`, then `npm run docs`. Follow `.agents/skills/merlon-docs/`.

## Figma UI Map

Editing the Finance Overhaul Figma library (UI Map / Foundations / Components): follow
`.agents/skills/figma-fo-ui/` (and `.cursor/rules/figma-fo-ui.mdc`). No overlapping pins; pin grids
sit under screens; hug layout only.
