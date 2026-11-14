# Finance Overhaul

**Cities: Skylines I** mod that turns the Financial Districts Stock Exchange into city treasury
management — index funds, sector ETFs, dividend funds, municipal bonds, automated orders, and IPOs.

> Status: **MVP** — reflective stock hooks, optional +1% probe bias, and live reload. Broader
> instruments are documented under `docs/features/` and not shipped yet.

## Why this exists

Vanilla Financial Districts asks you to babysit thirteen volatile stocks. This mod aims for a
passive treasury layer that still ties returns to how you build the city.

## Getting started (MVP)

Cloud agents can land docs and unit tests. **Building against Cities and loading the mod into the
game requires a local clone** on a machine with the game installed — see
[Local game loop](docs/developer/local-game-loop.md).

1. Clone this repo locally; `npm install` && `npm test`.
2. Subscribe to [Cities Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402).
3. Deploy: `./scripts/install-mod-local.sh --dev` (see
   [local MVP install](docs/developer/local-mvp-install.md)).
4. Enable **Finance Overhaul** in Content Manager.
5. Load a city with a Stock Exchange. Leave **MVP probe bias** off unless you are verifying the
   hook.

Live reload: rebuild with `--dev` so `AssemblyVersion` changes — see
[Live reload](docs/developer/live-reload.md).

## Docs

Sharded docs via [Merlon Docs](https://github.com/Merlon-Docs/merlon):

```bash
npm install
npm run docs
npm test
```

| Guide       | Path                                 |
| ----------- | ------------------------------------ |
| Features    | [`docs/features/`](docs/features/)   |
| Player      | [`docs/client/`](docs/client/)       |
| Contributor | [`docs/developer/`](docs/developer/) |
| Glossary    | [`docs/glossary/`](docs/glossary/)   |

Product vision for funds, bonds, automation, and IPOs:
[Market analogs](docs/features/market-analogs.md).

## Guidelines

Repository layout, Harmony separation, and assembly live-reload identity follow
[CSL-TrackpadCameraControl](https://github.com/betsalel-williamson/CSL-TrackpadCameraControl).

## License

MIT — see [LICENSE](LICENSE).

## Disclaimers and trademarks

Unofficial fan project. Not affiliated with Paradox Interactive or Colossal Order. Cities: Skylines
and Financial Districts are trademarks of their respective owners.
