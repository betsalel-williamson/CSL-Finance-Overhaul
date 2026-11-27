#!/usr/bin/env bash
# Finishes a dev recording (FinanceOverhaul-video-NAME/, saved by the --dev mod's FrameRecorder:
# scenario steps `record NAME [CONTROL]` / `record-stop`, or the FinanceOverhaul.record trigger
# file): JPEG frames become an MP4 at the pace they were shown (a video recording already is
# one), plus contact sheets to look through.
# Usage: scripts/frames-to-video.sh NAME|FOLDER [--sheets] [--gif]
#   --sheets  also write sheet-001.png … (every frame in order, 4 × 4 per sheet)
#   --gif     also write NAME.gif (half size, 15 fps)
set -euo pipefail
GAME_DIR="$HOME/Library/Application Support/Colossal Order/Cities_Skylines"
if [[ $# -lt 1 ]]; then
  sed -n '2,8p' "$0"
  exit 2
fi
src="$1"
shift
[[ -d "$src" ]] || src="$GAME_DIR/FinanceOverhaul-video-$src"
[[ -f "$src/frames.txt" ]] || { echo "no frames.txt in $src (still recording?)" >&2; exit 1; }
command -v ffmpeg >/dev/null || { echo "ffmpeg is needed (brew install ffmpeg)" >&2; exit 1; }
name="$(basename "$src")"
out="$src/$name.mp4"
if ls "$src"/frame-*.jpg >/dev/null 2>&1; then
  frames=$(ls "$src"/frame-*.jpg | wc -l | tr -d ' ')
  # JPEG recording: the concat list's durations are the real time each frame was on screen.
  (cd "$src" && ffmpeg -loglevel error -y -f concat -safe 0 -i frames.txt \
    -fps_mode vfr -pix_fmt yuv420p -c:v libx264 -crf 20 "$name.mp4")
  input=(-framerate 1 -i frame-%05d.jpg)
else
  # Video recording: the game already wrote the MP4 (every shown frame once).
  [[ -f "$out" ]] || { echo "no frames and no $name.mp4 in $src" >&2; exit 1; }
  frames=$(wc -l < "$src/frames.txt" | tr -d ' ')
  input=(-i "$name.mp4")
fi
echo "video:  $out ($frames frames)"
for arg in "$@"; do
  case "$arg" in
    --sheets)
      (cd "$src" && rm -f sheet-*.png && ffmpeg -loglevel error -y "${input[@]}" \
        -vf "scale=iw/2:-2,tile=4x4:padding=4" -fps_mode passthrough \
        sheet-%03d.png)
      echo "sheets: $(ls "$src"/sheet-*.png | wc -l | tr -d ' ') in $src (16 frames each, left to right: sheet k holds frames 16k−15 … 16k)"
      ;;
    --gif)
      (cd "$src" && ffmpeg -loglevel error -y -i "$name.mp4" \
        -vf "fps=15,scale=iw/2:-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse" "$name.gif")
      echo "gif:    $src/$name.gif"
      ;;
    *) echo "unknown option $arg" >&2; exit 2 ;;
  esac
done
