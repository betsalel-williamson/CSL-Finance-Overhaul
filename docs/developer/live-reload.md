# Live reload

Cities can reload a mod when `AssemblyVersion` changes (Paradox Advanced Mod Setup → Automate).

## How this repo does it

1. `AssemblyVersion` uses `Major.Minor.*` with non-deterministic builds so each compile gets a new
   build/revision ([ADR 0002](../features/adr/0002-live-reload-identity.md)).
2. `./scripts/install-mod-local.sh --dev` sets `DevBuildIdentity=true` so Options show the assembly
   identity and build timestamp.
3. Rebuild and reinstall; Automate picks up the new DLL. If Automate is off, restart Cities or
   toggle the mod.

## Sanity check

Options title with `--dev` should change build/revision between installs. If it does not, you are
looking at a stale Mods copy.

## Dev captures (`--dev` builds)

Trigger files next to `FinanceOverhaul.settings.txt`
(`~/Library/Application Support/Colossal Order/Cities_Skylines/` on macOS) let you check visuals
without touching the game window:

| Trigger file              | Effect                                                                                                                                      |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `FinanceOverhaul.capture` | Saves the rendered frame to `FinanceOverhaul-capture.png`.                                                                                  |
| `FinanceOverhaul.focus`   | Frames the facade of Stock Exchange _k_ of _n_ in the loaded city (text: optional `k`, default 1; add `capture` to save a frame after 3 s). |
| `FinanceOverhaul.order`   | Order commands: `buy SHARES LIMIT NAME` (prefab name or ticker symbol, e.g. `buy 5 900 Oil`), `cancel ID`, `list`; results in the mod log.  |
| `FinanceOverhaul.record`  | Video capture: `start NAME [CONTROL]` saves every shown frame (whole screen, or one UI control's rectangle) until `stop` or 30 s.           |

If the city has no Stock Exchange, `focus` logs `dev focus: this city has no Stock Exchange` and
leaves the camera alone. Example:

```bash
echo "1 capture" > ~/Library/Application\ Support/Colossal\ Order/Cities_Skylines/FinanceOverhaul.focus
```

### Video capture (every shown frame)

A still capture misses a glitch that lasts a frame or two (a line drawn one row late just after a
scroll). `FrameRecorder` (dev builds only) reads the screen at the end of each frame, so the frames
are what the player saw, and saves them as JPEGs in `FinanceOverhaul-video-NAME/` with `frames.txt`
(how long each was on screen). Cropping to a control (`FinanceOverhaulInvestmentsList`) keeps them
small. The log notes the game's frame rate when the recording starts and the frames saved when it
stops; saving costs a few ms per frame, so a busy city records about as fast as it draws. Then:

```bash
scripts/frames-to-video.sh NAME --sheets
```

writes `NAME.mp4` at the real pace (ffmpeg's concat durations) and contact sheets of 16 frames in
order (`--gif` adds a half-size GIF). In a scenario: `record NAME [CONTROL]` … `record-stop`, with
`investments-scroll-smooth PIXELS SECONDS` to scroll a little every frame as a wheel does; scenario
81 records the list scrolling and the overview flipping through its views.
