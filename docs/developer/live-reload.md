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
are what the player saw. With ffmpeg installed (`/opt/homebrew/bin`, `/usr/local/bin` or `/usr/bin`)
the raw pixels go to a worker thread that streams them into one ffmpeg process, which encodes H.264
on the Mac's VideoToolbox: `FinanceOverhaul-video-NAME/FinanceOverhaul-video-NAME.mp4`, every shown
frame once at the game's frame rate when the recording started, and `frames.txt` with when each
frame was shown. Without ffmpeg (or after `record-format jpeg`) each frame is a JPEG instead.
Cropping to a control (`FinanceOverhaulInvestmentsList`) keeps frames small. A frame that finds 90
still waiting for ffmpeg is dropped and counted, so a slow disk never stalls the game.

What it costs (measured 2026-11-27 in a city of 140,000 at about 14 fps; the log's stop line gives
the recorder's main-thread time per frame):

| `record-format` | What runs each frame                | Main thread per frame |
| --------------- | ----------------------------------- | --------------------- |
| `count`         | frames counted, nothing read        | —                     |
| `probe`         | a 16 × 16 read, nothing kept        | ~22 ms                |
| `jpeg`          | full read, JPEG encoded in the game | ~31 ms                |
| `video`         | full read, raw pixels to ffmpeg     | ~32 ms                |

Almost all of it is the read: Unity 5.6 has no asynchronous GPU read (`AsyncGPUReadback` came in
2018.1), so `ReadPixels` makes the CPU wait for the GPU to finish the frame, however small the read.
A GPU copy into a ring of render textures read two frames later waited just as long (tried and
removed). Encoding is cheap by comparison, so the ffmpeg path is about the files (one MP4, no
stitching), not speed. The game slows by about a third while recording; every saved frame is still
one the player saw. A recorder that costs the game nothing has to capture outside it (macOS screen
recording), which needs the Screen Recording permission.

Then:

```bash
scripts/frames-to-video.sh NAME --sheets
```

makes `NAME.mp4` from JPEG frames at the real pace (a video recording already has it) and contact
sheets of 16 frames in order (`--gif` adds a half-size GIF). In a scenario: `record NAME [CONTROL]`
… `record-stop` (`record-format video|jpeg|count|probe` first to choose), with
`investments-scroll-smooth PIXELS SECONDS` to scroll a little every frame as a wheel does; scenario
81 records the list scrolling and the overview flipping through its views.
