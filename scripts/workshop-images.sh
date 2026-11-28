#!/usr/bin/env bash
# Builds the Steam Workshop images (1920 × 1080) from in-game captures: a title band in the mod's
# window colour with a cyan accent, and the cropped screenshot under it. Five feature images and a
# sixth that sends players to the online guide and asks them to rate, favourite and comment.
#
# Captures come from the --dev mod in the game's settings folder: the store-* ones from
# tests/game-ui/store/captures.uitest, led-exchange from the FinanceOverhaul.focus trigger
# ("1 capture", copied to FinanceOverhaul-uitest-led-exchange.png), the rest from the scenarios
# named in docs/developer/workshop-storefront.md.
# Usage: scripts/workshop-images.sh [OUT_DIR]   (default docs/developer/img/workshop)
set -euo pipefail
GAME="${FO_CAPTURES:-$HOME/Library/Application Support/Colossal Order/Cities_Skylines}"
OUT="${1:-$(cd "$(dirname "$0")/.." && pwd)/docs/developer/img/workshop}"
BOLD="${FO_FONT_BOLD:-$HOME/Library/Fonts/OpenSans-Bold.ttf}"
REG="${FO_FONT:-$HOME/Library/Fonts/OpenSans-Regular.ttf}"
command -v magick >/dev/null || { echo "ImageMagick is needed (brew install imagemagick)" >&2; exit 1; }
mkdir -p "$OUT"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

BG="rgb(9,14,17)"        # FoTheme.Window
ACCENT="rgb(101,214,248)" # FoTheme.Accent
MUTED="rgb(159,176,192)"  # FoTheme.TickerFlat

cap() { echo "$GAME/FinanceOverhaul-uitest-$1.png"; }

# band TITLE SUBTITLE OUT: the 1920 × 150 title band.
band() {
  magick -size 1920x150 "xc:$BG" \
    -fill "$ACCENT" -draw "rectangle 0,0 11,150" \
    -font "$BOLD" -pointsize 54 -fill white -annotate +56+70 "$1" \
    -font "$REG" -pointsize 30 -fill "$MUTED" -annotate +58+118 "$2" "$3"
}

# shot NAME TITLE SUBTITLE CAPTURE CROP: one screenshot filling the 1920 × 930 under the band.
shot() {
  band "$2" "$3" "$TMP/band.png"
  magick "$4" -crop "$5" +repage -resize 1920x930^ -gravity center -extent 1920x930 "$TMP/body.png"
  magick "$TMP/band.png" "$TMP/body.png" -append -quality 92 "$OUT/$1.jpg"
  echo "wrote $OUT/$1.jpg"
}

# pair NAME TITLE SUBTITLE CAP1 CROP1 CAP2 CROP2: two tall crops side by side on the window colour.
pair() {
  band "$2" "$3" "$TMP/band.png"
  # Each crop as large as fits its half (a close crop of one feature), centred on the window colour.
  magick "$4" -crop "$5" +repage -resize 900x870 "$TMP/a.png"
  magick "$6" -crop "$7" +repage -resize 900x870 "$TMP/b.png"
  magick -background "$BG" -gravity center "$TMP/a.png" -size 40x10 "xc:$BG" "$TMP/b.png" \
    +append "$TMP/ab.png"
  magick -size 1920x930 "xc:$BG" "$TMP/ab.png" -gravity center -composite "$TMP/body.png"
  magick "$TMP/band.png" "$TMP/body.png" -append -quality 92 "$OUT/$1.jpg"
  echo "wrote $OUT/$1.jpg"
}

# Close crops: each image shows the feature it names and little else.
shot 1-investments-tab \
  "One Investments tab" \
  "Every stock in one list, with your holdings and orders - and the market beside it" \
  "$(cap store-investments-market)" 1280x610+340+155

pair 2-overview \
  "See the whole market at a glance" \
  "Heatmap sized by market value, and the city figure behind every stock" \
  "$(cap store-heatmap)" 580x600+985+160 \
  "$(cap investments-overview-city)" 580x600+985+160

pair 3-local-ipos \
  "Your city's businesses go public" \
  "Local IPOs: buy in at the offering price, then follow the company from its building" \
  "$(cap ipo-dialog-area)" 590x500+680+365 \
  "$(cap area-home-pog)" 640x640+960+80

pair 4-orders-and-chirps \
  "Orders that trade while you build" \
  "Limit, stop-loss, take-profit and trailing stop - and a Chirps tab for every stock" \
  "$(cap invest-dialog-sell-side)" 660x920+650+150 \
  "$(cap store-chirps)" 660x880+640+170

shot 5-led-ticker \
  "Live prices on the Stock Exchange" \
  "LED ticker boards scroll every stock, in %% or points as your pills show them" \
  "$(cap led-exchange)" 1240x600+340+280

# 6: the guide, and a word for the Workshop page.
magick -size 1920x1080 "xc:$BG" \
  -fill "$ACCENT" -draw "rectangle 0,0 11,1080" \
  -font "$BOLD" -pointsize 76 -fill white -annotate +120+230 "There's more in the player guide" \
  -font "$REG" -pointsize 38 -fill "$MUTED" \
  -annotate +124+320 "Every feature, every option and how your saves stay safe, step by step:" \
  -font "$BOLD" -pointsize 44 -fill "$ACCENT" \
  -annotate +124+420 "betsalel-williamson.github.io/CSL-Finance-Overhaul" \
  -font "$REG" -pointsize 36 -fill white \
  -annotate +124+540 "Investments tab and overview  ·  Local IPOs  ·  Orders and Chirps" \
  -annotate +124+600 "Stock Monitor  ·  LED ticker  ·  Index stock  ·  City bonds  ·  Options" \
  -fill "rgb(30,36,44)" -draw "roundrectangle 120,720 1800,940 18,18" \
  -font "$BOLD" -pointsize 50 -fill white -annotate +170+810 "Enjoying Finance Overhaul?" \
  -font "$REG" -pointsize 36 -fill "$MUTED" \
  -annotate +172+880 "Please rate it, add it to your favourites and leave a comment - it really helps." \
  -quality 92 "$OUT/6-player-guide.jpg"
echo "wrote $OUT/6-player-guide.jpg"
