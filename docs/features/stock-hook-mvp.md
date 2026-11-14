# Stock hook MVP

## Value

Contributors can prove the mod attaches to Financial Districts stock pricing and applies a
reversible probe change without rewriting the vanilla market.

## Scope

| In                                                                                     | Out                                                          |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| Discover stock-related types via reflection                                            | Full ETF / bond / IPO product UI                             |
| Optional MVP price bias (default off / identity)                                       | Persistent portfolio automation                              |
| Fail soft when Financial Districts or Harmony is missing                               | Hard dependency that crashes Content Manager without the DLC |
| Live reload via assembly identity (see [ADR 0002](./adr/0002-live-reload-identity.md)) | Workshop publish polish                                      |

## Contracts

1. **Enable** — With Cities Harmony and Financial Districts present, enabling the mod registers
   Harmony patches and logs a one-line hook summary.
2. **Probe bias** — When the Options toggle is on, each hooked price refresh multiplies the observed
   share price by the configured bias factor (default `1.01`). When off, prices are unchanged.
3. **Fail soft** — Missing DLC, missing Harmony, or unknown type shapes log a clear message and
   leave vanilla trading alone.
4. **Reload** — Rebuilding with `./scripts/install-mod-local.sh --dev` changes `AssemblyVersion` so
   Cities Automate can reload the DLL without a full Content Manager dance when that path is
   configured.

## Acceptance

- Unit tests cover bias math and index-weight helpers without Cities assemblies.
- Local install script deploys `FinanceOverhaul.dll` into the Mods folder.
- Options title shows product semver by default; `--dev` shows assembly identity for reload QA.
