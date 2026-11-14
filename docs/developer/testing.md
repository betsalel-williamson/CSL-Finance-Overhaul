# Testing

## Layers

| Project                        | Proves                                      | Cities?       |
| ------------------------------ | ------------------------------------------- | ------------- |
| `FinanceOverhaul.Market.Tests` | Index math, ticker, settings, perf budgets  | No            |
| `FinanceOverhaul.Mod.Tests`    | Product/assembly identity for live reload   | No (refs off) |
| Local game + Automate          | Harmony patches against Financial Districts | Yes           |

## Commands

```bash
npm run test:unit              # Market only — agents/CI default
npm run test:mod               # Mod host without Cities
npm test                       # both
npm run hooks:agent-validate   # full trunk gate
npm run test:game              # game UI scenarios (running game, --dev build)
```

### Game UI scenarios

`scripts/game-ui-test.sh` copies each `tests/game-ui/*.uitest` script into the mod's
`FinanceOverhaul.uitest` trigger; the mod (`UiTestRunner`, `--dev` only) drives the real UI —
`show-buy`, `click`, `type` (focus, type, tab out), `run-days`, `set-flag <OrderKind|all> on|off`
(same as ticking an order type's Options checkbox) — and checks `expect-visible`, `expect-text`,
`expect-no-overflow` (every visible part inside the dialog, and none of our fixed-width labels
cutting its text off), `expect-owned-delta`, `expect-orders-delta` (orders this scenario created)
and `expect-log`. Lines after `cleanup:` always run (cancel orders, sell back, close the dialog).
Results: `uitest: PASS|FAIL` in the mod log, captures as `FinanceOverhaul-uitest-*.png`. Scenarios
trade small amounts in the loaded city and undo them in cleanup; use a test save.

Performance tests in `PriceBiasPerformanceTests` budget the equal-weight index (best of nine
batches, so a busy machine cannot fail them) and assert it allocates nothing. They measure Market
CPU only, not Cities frame time.

## Layer rule

`src/FinanceOverhaul.Market` must not reference Cities types — see
[ADR 0003](../features/adr/0003-market-library-vs-mod-surface.md) and `npm run layer:lint`.
