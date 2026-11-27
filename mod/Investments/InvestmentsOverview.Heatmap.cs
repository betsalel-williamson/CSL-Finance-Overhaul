using System; // Array
using System.Collections.Generic; // List
using ColossalFramework.UI; // UIComponent, UILabel, UIPanel, UITextureSprite
// FinanceOverhaul.Core: HeatRect, HeatTile, HeatmapOverview, MonitorQuote, StockMonitor, Strings
using FinanceOverhaul.Core;
using UnityEngine; // Color32, Mathf, Texture2D, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// The Heatmap view: every stock as a tile coloured from orange (falling) to green
    /// (rising) by its change today or over 7 or 30 days, the city's holdings outlined in the
    /// accent, the scale under the tiles. A click on a tile opens the stock's dialog. Order,
    /// scale and grid come from <see cref="HeatmapOverview"/>.
    /// </summary>
    internal sealed partial class InvestmentsOverview
    {
        private const float TileGap = 4f;
        private const float LegendH = 22f;
        private const int ScaleWidth = 120;

        private UIPanel heatmap;
        private UILabel heatHeading;
        private UILabel heatIndexKey;
        private UILabel heatIndex;
        private FoToggleTabs heatSpan;
        private UIPanel heatArea;
        private readonly List<HeatTileView> heatTiles = new List<HeatTileView>();
        private UILabel scaleLow;
        private UILabel scaleHigh;
        private UITextureSprite scaleBar;
        private Texture2D scaleTexture;

        /// <summary>The span the heatmap colours by: 1 (today), 7 or 30 days.</summary>
        private static int HeatSpan
        {
            get
            {
                return HeatmapOverview.Span(
                    Mod.Settings == null
                        ? HeatmapOverview.Today
                        : Mod.Settings.InvestmentHeatmapDays
                );
            }
            set
            {
                if (Mod.Settings != null)
                {
                    Mod.Settings.InvestmentHeatmapDays = value;
                    Mod.SaveSettings();
                }
            }
        }

        private void BuildHeatmap()
        {
            heatmap = View("Heatmap");
            heatHeading = Heading(heatmap, "", Pad, Pad + 2f);
            heatHeading.name = Name + "HeatmapHeading";
            heatHeading.tooltip = Strings.Get("overview.heatmap.sizeTip");
            // The index fund averages the tiles: its change sits by the heading, not as a tile.
            heatIndexKey = FoText.Label(
                heatmap,
                Name + "HeatmapIndexKey",
                "",
                0.75f,
                FoTheme.TextMuted
            );
            heatIndex = FoPill.AddToggle(
                heatmap,
                Name + "HeatmapIndex",
                new Vector2(62f, 17f),
                0.7f,
                FoTheme.Header,
                Strings.Get("monitor.pill.tip"),
                togglePills
            );

            var labels = new string[HeatmapOverview.Spans.Length];
            var widths = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = HeatmapOverview.SpanTab(HeatmapOverview.Spans[i]);
                widths[i] = i == 0 ? 52f : 40f;
            }

            heatSpan = FoToggleTabs.Add(
                heatmap,
                Name + "HeatmapSpan",
                labels,
                widths,
                20f,
                0.7f,
                i =>
                {
                    HeatSpan = HeatmapOverview.Spans[i];
                    drawn = null;
                    Draw();
                }
            );
            heatSpan.Secondary();
            for (int i = 0; i < labels.Length; i++)
            {
                heatSpan[i].name = Name + "HeatmapSpan-" + HeatmapOverview.Spans[i];
            }

            heatSpan.Panel.relativePosition = new Vector3(heatmap.width - Pad - 132f, Pad);

            heatArea = heatmap.AddUIComponent<UIPanel>();
            heatArea.name = Name + "HeatmapTiles";
            heatArea.relativePosition = new Vector3(Pad, Pad + 30f);
            heatArea.size = new Vector2(
                heatmap.width - 2f * Pad,
                heatmap.height - Pad - 30f - Pad - LegendH
            );

            // The scale: orange … grey … green, its ends in %, and the outline's key.
            float legendY = heatmap.height - Pad - LegendH + 6f;
            scaleLow = FoText.Label(heatmap, Name + "HeatmapLow", "", 0.65f, FoTheme.ChartLabel);
            scaleLow.relativePosition = new Vector3(Pad, legendY);
            scaleTexture = FoTexture.Canvas(ScaleWidth, 1);
            for (int x = 0; x < ScaleWidth; x++)
            {
                scaleTexture.SetPixel(x, 0, FoTheme.Heat(x / (float)(ScaleWidth - 1) * 2f - 1f));
            }

            scaleTexture.Apply();
            textures.Add(scaleTexture);
            scaleBar = heatmap.AddUIComponent<UITextureSprite>();
            scaleBar.name = Name + "HeatmapScale";
            scaleBar.texture = scaleTexture;
            scaleBar.size = new Vector2(ScaleWidth, 8f);
            scaleBar.isInteractive = false;
            scaleBar.tooltip = Strings.Get("overview.heatmap.scale");
            scaleHigh = FoText.Label(heatmap, Name + "HeatmapHigh", "", 0.65f, FoTheme.ChartLabel);

            UILabel heldKey = FoText.Label(
                heatmap,
                Name + "HeatmapHeldKey",
                Strings.Get("overview.heatmap.held"),
                0.65f,
                FoTheme.ChartLabel
            );
            heldKey.relativePosition = new Vector3(heatmap.width - Pad - heldKey.width, legendY);
            UIPanel swatch = heatmap.AddUIComponent<UIPanel>();
            swatch.name = Name + "HeatmapHeldSwatch";
            swatch.backgroundSprite = "EmptySprite"; // not shown: a game sprite name
            swatch.color = FoTheme.Accent;
            swatch.size = new Vector2(12f, 10f);
            swatch.relativePosition = new Vector3(heldKey.relativePosition.x - 17f, legendY + 1f);
            swatch.isInteractive = false;
            UIPanel hole = swatch.AddUIComponent<UIPanel>();
            hole.backgroundSprite = "EmptySprite"; // not shown: a game sprite name
            hole.color = FoTheme.HeatFlat;
            hole.size = new Vector2(8f, 6f);
            hole.relativePosition = new Vector3(2f, 2f);
            hole.isInteractive = false;
        }

        private void DrawHeatmap()
        {
            int span = HeatSpan;
            bool inPoints = pillsInPoints();
            heatSpan.Choose(Array.IndexOf(HeatmapOverview.Spans, span));
            heatHeading.text = HeatmapOverview.Heading(span);
            MonitorQuote fund = HeatmapOverview.IndexFund(quotes);
            heatIndexKey.isVisible = fund != null;
            heatIndex.isVisible = fund != null;
            if (fund != null)
            {
                heatIndexKey.text = fund.Symbol;
                heatIndexKey.relativePosition = new Vector3(
                    heatHeading.relativePosition.x + heatHeading.width + 12f,
                    heatHeading.relativePosition.y + 1f
                );
                heatIndex.text = HeatmapOverview.IndexChange(quotes, span, inPoints);
                heatIndex.color = FoTheme.Change(
                    StockMonitor.ChangeColour(HeatmapOverview.Change(fund, span))
                );
                heatIndex.relativePosition = new Vector3(
                    heatIndexKey.relativePosition.x + heatIndexKey.width + 6f,
                    heatHeading.relativePosition.y
                );
                heatIndex.tooltip = Strings.Format(
                    "overview.heatmap.indexTip",
                    fund.Name ?? fund.Symbol
                );
            }

            List<HeatTile> tiles = HeatmapOverview.Tiles(quotes, span);
            HeatRect[] rects = HeatmapOverview.Treemap(
                tiles.ConvertAll(t => t.Weight),
                heatArea.width,
                heatArea.height,
                TileGap
            );
            while (heatTiles.Count < tiles.Count)
            {
                heatTiles.Add(new HeatTileView(heatArea, heatTiles.Count));
            }

            for (int i = 0; i < heatTiles.Count; i++)
            {
                if (i >= tiles.Count)
                {
                    heatTiles[i].Hide();
                    continue;
                }

                // Whole pixels, the gap kept: each edge rounded on its own.
                HeatRect r = rects[i];
                float x0 = Mathf.Round(r.X),
                    y0 = Mathf.Round(r.Y);
                heatTiles[i]
                    .Show(
                        tiles[i],
                        HeatmapOverview.TileText(tiles[i], inPoints),
                        new Vector3(x0, y0),
                        new Vector2(
                            Mathf.Max(1f, Mathf.Round(r.X + r.Width) - x0),
                            Mathf.Max(1f, Mathf.Round(r.Y + r.Height) - y0)
                        )
                    );
            }

            float scale = HeatmapOverview.Scale(tiles);
            float legendY = heatmap.height - Pad - LegendH + 6f;
            scaleLow.text = HeatmapOverview.ScaleEnd(scale, false);
            scaleBar.relativePosition = new Vector3(Pad + scaleLow.width + 6f, legendY + 2f);
            scaleHigh.text = HeatmapOverview.ScaleEnd(scale, true);
            scaleHigh.relativePosition = new Vector3(
                scaleBar.relativePosition.x + ScaleWidth + 6f,
                legendY
            );
        }

        /// <summary>
        /// One tile: its heat colour, an accent outline when held. A big tile reads as in the
        /// design: the symbol top left, the change large at the bottom left, the price (and
        /// the city's shares) under it; a middling one the symbol and change centred; a small
        /// one the symbol alone; a sliver nothing (its tooltip still names it).
        /// </summary>
        private sealed class HeatTileView
        {
            private const float Outline = 2f;
            private const float BigWidth = 96f;
            private const float BigHeight = 70f;

            private readonly UIPanel back;
            private readonly UIPanel face;
            private readonly UILabel symbol;
            private readonly UILabel change;
            private readonly UILabel detail;
            private MonitorQuote quote;

            public HeatTileView(UIComponent parent, int index)
            {
                string name = Name + "Heat-" + index;
                back = parent.AddUIComponent<UIPanel>();
                back.name = name;
                back.backgroundSprite = "GenericPanelWhite";
                back.eventClick += (c, p) => OpenDialog(quote);
                face = back.AddUIComponent<UIPanel>();
                face.name = name + "Face";
                face.backgroundSprite = "GenericPanelWhite";
                face.isInteractive = false;
                face.clipChildren = true;
                symbol = FoText.Label(face, name + "Symbol", "", 0.8f, FoTheme.GameText);
                symbol.isInteractive = false;
                change = FoText.Label(face, name + "Change", "", 0.7f, FoTheme.GameText);
                change.isInteractive = false;
                detail = FoText.Label(face, name + "Detail", "", 0.7f, FoTheme.GameText);
                detail.isInteractive = false;
            }

            public void Show(HeatTile tile, string text, Vector3 at, Vector2 size)
            {
                quote = tile.Quote;
                back.isVisible = true;
                // Named for the scenarios: the tile of a stock is found by its symbol.
                back.name = Name + "Heat-" + tile.Quote.Symbol;
                symbol.name = back.name + "Symbol";
                change.name = back.name + "Change";
                detail.name = back.name + "Detail";
                back.relativePosition = at;
                back.size = size;
                Color32 colour = tile.Locked ? FoTheme.SectionFill : FoTheme.Heat(tile.Heat);
                back.color = tile.Held ? FoTheme.Accent : colour;
                face.color = colour;
                float inset = tile.Held && size.x > 8f && size.y > 8f ? Outline : 0f;
                face.relativePosition = new Vector3(inset, inset);
                face.size = new Vector2(size.x - 2f * inset, size.y - 2f * inset);
                back.tooltip = HeatmapOverview.Tip(tile);

                Color32 ink = tile.Locked ? FoTheme.TextMuted : FoTheme.HeatInk(colour);
                symbol.textColor = ink;
                change.textColor = ink;
                detail.textColor = ink;
                // Dark text on a bright tile goes without the shadow that keeps white text sharp.
                bool shadow = !ink.Equals(FoTheme.HeatInkDark);
                symbol.useDropShadow = shadow;
                change.useDropShadow = shadow;
                detail.useDropShadow = shadow;
                symbol.text = tile.Quote.Symbol;
                change.text = text;
                detail.text = tile.Locked ? string.Empty : HeatmapOverview.Detail(tile);
                float w = face.width,
                    h = face.height;
                if (w >= BigWidth && h >= BigHeight)
                {
                    Big(w, h);
                }
                else
                {
                    Small(w, h);
                }
            }

            private void Big(float w, float h)
            {
                const float edge = 8f;
                float room = w - 2f * edge;
                if (!Scale(symbol, 1.05f, room) || !Scale(change, 1.15f, room))
                {
                    Small(w, h); // too narrow for the big layout's text: as a middling tile
                    return;
                }

                symbol.isVisible = true;
                change.isVisible = true;
                // The price line: with the city's shares, else the price alone, else nothing (the
                // tooltip still has both).
                bool fits = detail.text.Length > 0 && Scale(detail, 0.7f, room);
                if (!fits && detail.text.Length > 0 && quote != null)
                {
                    detail.text = StockMonitor.PriceText(quote.Price);
                    fits = Scale(detail, 0.7f, room);
                }

                detail.isVisible = fits;
                symbol.relativePosition = new Vector3(edge, 6f);
                float bottom = h - 6f;
                if (detail.isVisible)
                {
                    detail.relativePosition = new Vector3(
                        edge,
                        Mathf.Round(bottom - detail.height)
                    );
                    bottom -= detail.height;
                }

                change.relativePosition = new Vector3(edge, Mathf.Round(bottom - change.height));
            }

            private void Small(float w, float h)
            {
                float room = w - 6f;
                detail.isVisible = false;
                bool symbolFits = Scale(symbol, 0.8f, room) && symbol.height <= h;
                bool both = Scale(change, 0.7f, room) && h >= symbol.height + change.height + 2f;
                symbol.isVisible = symbolFits;
                change.isVisible = symbolFits && both;
                float top = change.isVisible
                    ? (h - symbol.height - change.height) / 2f
                    : (h - symbol.height) / 2f;
                symbol.relativePosition = new Vector3(
                    Mathf.Round((w - symbol.width) / 2f),
                    Mathf.Round(top)
                );
                change.relativePosition = new Vector3(
                    Mathf.Round((w - change.width) / 2f),
                    Mathf.Round(top + symbol.height)
                );
            }

            /// <summary>The label at its scale, shrunk (not below half) to fit the room; false when it cannot.</summary>
            private static bool Scale(UILabel label, float scale, float room)
            {
                SetScale(label, scale);
                if (label.width > room)
                {
                    SetScale(label, Mathf.Max(0.5f * scale, scale * room / label.width));
                }

                return label.width <= room + 0.5f;
            }

            /// <summary>Sets the text scale and has the label measure itself again at once.</summary>
            private static void SetScale(UILabel label, float scale)
            {
                label.autoSize = false;
                label.textScale = scale;
                label.autoSize = true;
            }

            public void Hide()
            {
                back.isVisible = false;
                quote = null;
            }
        }
    }
}
