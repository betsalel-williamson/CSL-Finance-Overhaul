# Contributing

**Finance Overhaul** is early MVP work. Open an issue before large PRs.

## Local setup

After cloud scaffold PRs merge (or while reviewing them), do in-game work from a **local clone** —
Cities managed assemblies are not available in the cloud agent. Full steps:
[Local game loop](docs/developer/local-game-loop.md).

```bash
npm install
npm run hooks:agent-validate
./scripts/install-mod-local.sh --dev
```

Trunk protocol: [Trunk-based development](docs/developer/trunk-based.md). Test layers:
[Testing](docs/developer/testing.md). Also: [Local MVP install](docs/developer/local-mvp-install.md)
and [Live reload](docs/developer/live-reload.md).

Mod guidelines (layout, live reload, Harmony separation) follow patterns from
[CSL-TrackpadCameraControl](https://github.com/betsalel-williamson/CSL-TrackpadCameraControl). Docs
follow [Merlon Docs](https://github.com/Merlon-Docs/merlon). Agents: see [AGENTS.md](AGENTS.md).
