# Testing

## Layers

| Project                        | Proves                                            | Cities?       |
| ------------------------------ | ------------------------------------------------- | ------------- |
| `FinanceOverhaul.Market.Tests` | Price bias, index math, probe apply, perf budgets | No            |
| `FinanceOverhaul.Mod.Tests`    | Product/assembly identity for live reload         | No (refs off) |
| Local game + Automate          | Harmony patches against Financial Districts       | Yes           |

## Commands

```bash
npm run test:unit              # Market only — agents/CI default
npm run test:mod               # Mod host without Cities
npm test                       # both
npm run hooks:agent-validate   # full trunk gate
```

Performance tests in `PriceBiasPerformanceTests` budget equal-weight index and probe apply loops.
They measure Market CPU only, not Cities frame time.

## Layer rule

`src/FinanceOverhaul.Market` must not reference Cities types — see
[ADR 0003](../features/adr/0003-market-library-vs-mod-surface.md) and `npm run layer:lint`.
