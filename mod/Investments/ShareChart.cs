using System; // Exception
using System.Collections.Generic; // Dictionary, KeyValuePair, List
using System.Reflection; // FieldInfo
using ColossalFramework.UI; // UIComponent, UIGraph, UILabel, UIPanel, UITextureSprite, UIView
using FinanceOverhaul.Core; // Sparkline, StockDetail, StockMonitor, Strings
// UnityEngine: Color32, FilterMode, GameObject, Mathf, RectOffset, Texture2D, TextureFormat,
//   TextureWrapMode, Vector2, Vector3
using UnityEngine;

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   EconomyManager, Investment, InvestmentTooltip

namespace FinanceOverhaul
{
    /// <summary>
    /// A price chart in the Stock Monitor style (Figma band 10): line, shaded area, dashed line
    /// at yesterday's price, labelled ₡ grid, "N days ago … today". Drawn into one texture at
    /// 2× and redrawn only when its values change.
    /// </summary>
    public sealed class ShareChartView
    {
        public const string Prefix = "FinanceOverhaulShareChart";
        private const int Scale = 2;
        private const int Thickness = 4;
        private const int AxisCount = 4;

        public UIPanel Root { get; private set; }
        public UITextureSprite Sprite { get; private set; }
        public float[] Values { get; private set; }
        private Texture2D texture;
        private readonly List<UILabel> axis = new List<UILabel>();
        private UILabel from,
            to;
        private string drawn;
        private string symbol;
        private UIPanel hoverLine,
            hoverDot;
        private UILabel hoverTip;

        /// <summary>Builds the chart filling <paramref name="size"/> inside <paramref name="parent"/>.</summary>
        /// <param name="opaque">Draw our own background (false inside the game's chart panel,
        /// whose sprite then shows exactly as the game draws it).</param>
        public static ShareChartView Create(
            UIComponent parent,
            string name,
            Vector2 size,
            float labelScale,
            bool opaque = true
        )
        {
            var v = new ShareChartView();
            v.Root = parent.AddUIComponent<UIPanel>();
            v.Root.name = name;
            v.Root.size = size;
            v.Root.relativePosition = Vector3.zero;
            // UIGraph has no sprite atlas of its own: give the overlay the game's default one,
            // or its opaque background never draws and the vanilla graph shows through.
            v.Root.atlas = UIView.GetAView().defaultAtlas;
            v.Root.backgroundSprite = opaque ? "EmptySprite" : string.Empty;
            v.Root.color = FoTheme.GameChartBack;
            v.Root.isInteractive = false;

            float axisW = 56f * labelScale / 0.7f,
                footer = 18f * labelScale / 0.7f;
            var plot = new Vector2(size.x - axisW - 12f, size.y - footer - 12f);
            v.texture = new Texture2D(
                (int)plot.x * Scale,
                (int)plot.y * Scale,
                TextureFormat.ARGB32,
                false
            );
            v.texture.filterMode = FilterMode.Bilinear;
            v.texture.wrapMode = TextureWrapMode.Clamp;
            v.Sprite = v.Root.AddUIComponent<UITextureSprite>();
            v.Sprite.name = name + "Plot";
            v.Sprite.size = plot;
            v.Sprite.relativePosition = new Vector3(8f, 6f);
            v.Sprite.texture = v.texture;
            v.Sprite.isInteractive = false;

            for (int i = 0; i < AxisCount; i++)
            {
                UILabel l = FoText.Label(
                    v.Root,
                    name + "Axis-" + i,
                    "",
                    labelScale,
                    FoTheme.ChartLabel
                );
                l.isInteractive = false;
                v.axis.Add(l);
            }

            v.from = FoText.Label(v.Root, name + "From", "", labelScale, FoTheme.ChartLabel);
            v.from.relativePosition = new Vector3(8f, 6f + plot.y + 3f);
            v.from.isInteractive = false;
            v.to = FoText.Label(
                v.Root,
                name + "To",
                Strings.Get("chart.today"),
                labelScale,
                FoTheme.ChartLabel
            );
            v.to.isInteractive = false;
            v.to.relativePosition = new Vector3(
                8f + plot.x - 30f * labelScale / 0.7f,
                6f + plot.y + 3f
            );
            return v;
        }

        /// <summary>Draws these daily prices (oldest first); skipped when nothing changed.</summary>
        public void Draw(float[] values, Color32 colour, string symbol = null)
        {
            this.symbol = symbol;
            string key = Key(values, colour);
            if (key == drawn || texture == null)
            {
                return;
            }

            drawn = key;
            Values = values;
            int w = texture.width,
                h = texture.height;
            byte[] mask = Sparkline.Render(values, w, h, Thickness, fillArea: true);
            float hi = StockDetail.High(values),
                lo = StockDetail.Low(values);
            float[] ticks = StockDetail.AxisValues(values, AxisCount);
            var grid = new bool[h];
            for (int i = 0; i < AxisCount; i++)
            {
                bool shown = i < ticks.Length;
                axis[i].isVisible = shown;
                if (!shown)
                {
                    continue;
                }

                int row = Mathf.Clamp(Sparkline.RowOf(ticks[i], lo, hi, h, Thickness), 0, h - 1);
                grid[row] = true;
                axis[i].text = StockMonitor.PriceText(ticks[i]);
                axis[i].relativePosition = new Vector3(
                    Sprite.relativePosition.x + Sprite.width + 6f,
                    Sprite.relativePosition.y + row / (float)Scale - axis[i].height / 2f
                );
            }

            from.text = Strings.Plural("daysAgo", values.Length, values.Length.ToString());
            var px = new Color32[w * h];
            var dash = new Color32(colour.r, colour.g, colour.b, 200);
            var gridColour = FoTheme.ChartGrid;
            var clear = FoTheme.Clear;
            for (int y = 0; y < h; y++)
            {
                byte areaAlpha = (byte)(80 * (1f - y / (float)h));
                for (int x = 0; x < w; x++)
                {
                    byte m = mask[y * w + x];
                    Color32 c =
                        m == Sparkline.Line ? colour
                        : m == Sparkline.Baseline ? dash
                        : m == Sparkline.Area ? new Color32(colour.r, colour.g, colour.b, areaAlpha)
                        : clear;
                    if (grid[y] && m != Sparkline.Line && m != Sparkline.Baseline)
                    {
                        c = gridColour;
                    }

                    px[(h - 1 - y) * w + x] = c;
                }
            }

            texture.SetPixels32(px);
            texture.Apply();
        }

        /// <summary>
        /// Crosshair, dot and that day's price when <paramref name="mouse"/> (UI units) is over
        /// the plot, like the Stock Monitor. Returns false (and hides it) otherwise.
        /// </summary>
        public bool Hover(Vector2 mouse)
        {
            if (
                Root == null
                || Sprite == null
                || Values == null
                || Values.Length == 0
                || !Root.isVisible
            )
            {
                HideHover();
                return false;
            }

            Vector3 p = Sprite.absolutePosition;
            float lx = mouse.x - p.x,
                ly = mouse.y - p.y;
            if (lx < 0f || ly < 0f || lx > Sprite.width || ly > Sprite.height)
            {
                HideHover();
                return false;
            }

            int w = texture.width,
                h = texture.height;
            int i = Sparkline.IndexAt(lx * Scale, w, Values.Length);
            if (float.IsNaN(Values[i]) || float.IsInfinity(Values[i]))
            {
                HideHover();
                return false; // a day before the stock existed: nothing to show
            }

            float x = Sparkline.ColumnOf(i, w, Values.Length) / Scale;
            float y =
                Sparkline.RowOf(
                    Values[i],
                    StockDetail.Low(Values),
                    StockDetail.High(Values),
                    h,
                    Thickness
                ) / (float)Scale;
            if (hoverTip == null)
            {
                hoverLine = Box(FoTheme.HoverLine);
                hoverDot = Box(FoTheme.HoverDot);
                hoverDot.size = new Vector2(6f, 6f);
                hoverTip = FoText.Label(Root, Root.name + "HoverTip", "", 0.7f, FoTheme.GameText);
                hoverTip.backgroundSprite = "GenericPanelWhite";
                hoverTip.color = FoTheme.Tip;
                hoverTip.padding = new RectOffset(6, 6, 3, 2);
                hoverTip.isInteractive = false;
            }

            Vector3 at = Sprite.relativePosition;
            hoverLine.size = new Vector2(1f, Sprite.height);
            hoverLine.relativePosition = new Vector3(at.x + x, at.y);
            hoverDot.relativePosition = new Vector3(at.x + x - 3f, at.y + y - 3f);
            hoverTip.text = StockMonitor.HoverText(symbol, i, Values.Length, Values[i]);
            hoverTip.relativePosition = new Vector3(
                Mathf.Clamp(at.x + x - hoverTip.width / 2f, 2f, Root.width - hoverTip.width - 2f),
                at.y + 2f
            );
            hoverLine.isVisible = hoverDot.isVisible = hoverTip.isVisible = true;
            return true;
        }

        public void HideHover()
        {
            if (hoverTip != null && hoverTip.isVisible)
            {
                hoverLine.isVisible = hoverDot.isVisible = hoverTip.isVisible = false;
            }
        }

        private UIPanel Box(Color32 colour)
        {
            UIPanel b = Root.AddUIComponent<UIPanel>();
            b.backgroundSprite = "EmptySprite";
            b.color = colour;
            b.isInteractive = false;
            return b;
        }

        public void Destroy()
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }

            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
                Root = null;
            }
        }

        private static string Key(float[] values, Color32 colour)
        {
            var sb = new System.Text.StringBuilder(values.Length * 8);
            foreach (float v in values)
            {
                sb.Append(v.ToString("0.###")).Append(',');
            }

            return sb.Append(colour.r).Append(colour.g).ToString();
        }
    }

    /// <summary>
    /// Draws the game's "Share value" charts — the Invest dialog's and the Economy panel
    /// tooltip's — in the Stock Monitor style, inside the vanilla UIGraph (which stays in
    /// place, so layouts do not move; its own curve is cleared after each vanilla update). Postfixes on BuySellInvestmentPanel.UpdateBindings and
    /// InvestmentTooltip.Update; the tooltip runs every frame, so the chart redraws only when
    /// its stock or prices change. Behind the "Share charts" Options flag; removed on hot
    /// reload and when the mod is disabled.
    /// </summary>
    public static class ShareChartOverlay
    {
        private static readonly FieldInfo DialogGraph = Need(
            GameInternals.BuySell.Graph,
            "BuySellInvestmentPanel.m_graph"
        );
        private static readonly FieldInfo DialogId = Need(
            GameInternals.BuySell.InvestmentId,
            "BuySellInvestmentPanel.m_investmentID"
        );
        private static readonly FieldInfo TipGraph = Need(
            GameInternals.Tooltip.Graph,
            "InvestmentTooltip.m_graph"
        );
        private static readonly FieldInfo TipId = Need(
            GameInternals.Tooltip.InvestmentId,
            "InvestmentTooltip.m_investmentID"
        );

        /// <summary>UI units the game's chart panel stops short of the graph's bottom edge.</summary>
        private const float PanelBottomInset = 21f;

        private static readonly Dictionary<UIGraph, ShareChartView> Views =
            new Dictionary<UIGraph, ShareChartView>();
        private static bool failureLogged;
        private static bool hoverFailureLogged;

        public static bool IsReady
        {
            get
            {
                return DialogGraph != null && DialogId != null && TipGraph != null && TipId != null;
            }
        }

        public static void OnDialogBindings(BuySellInvestmentPanel panel)
        {
            Apply(
                panel == null ? null : DialogGraph.GetValue(panel) as UIGraph,
                panel == null ? -1 : (byte)DialogId.GetValue(panel),
                0.7f
            );
        }

        public static void OnTooltipUpdate(InvestmentTooltip tip)
        {
            Apply(
                tip == null ? null : TipGraph.GetValue(tip) as UIGraph,
                tip == null ? -1 : (byte)TipId.GetValue(tip),
                0.7f
            );
        }

        private static void Apply(UIGraph graph, int id, float labelScale)
        {
            if (graph == null || id < 0)
            {
                return;
            }

            try
            {
                if (Mod.Settings != null && !Mod.Settings.ShareChartsEnabled)
                {
                    Remove(graph);
                    return;
                }

                if (!graph.isVisible)
                {
                    return;
                }

                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                if (id >= buffer.Length || (buffer[id].m_flags & Investment.Flags.Created) == 0)
                {
                    return;
                }

                ShareChartView view;
                if (
                    !Views.TryGetValue(graph, out view)
                    || view.Root == null
                    || view.Root.width != graph.width
                )
                {
                    if (view != null)
                    {
                        view.Destroy();
                    }

                    RemoveStale(graph);
                    // The game's GraphParent sliced sprite draws its dark panel ~9 units wider than
                    // the graph on each side but stops ~21 units short of the bottom (measured in
                    // game at 1920×1200: rect 660–1259 × 320–598 px, visible panel 650–1268 ×
                    // 314–575 px). Use the game's panel as is and keep everything inside it.
                    view = ShareChartView.Create(
                        graph,
                        ShareChartView.Prefix,
                        new Vector2(graph.width, graph.height - PanelBottomInset),
                        labelScale,
                        opaque: false
                    );
                    Views[graph] = view;
                }

                var history = new float[Investment.kPastValueCount];
                buffer[id].FillGraphValues(history);
                history = CompanyListing.ChartDays(buffer[id].Info.m_investmentAI, history);
                float[] range = StockDetail.Range(history, StockDetail.LongRange);
                // One rule everywhere: the game's change rate (list, pills, Economy panel, LED).
                Color32 colour = FoTheme.Change(
                    StockMonitor.ChangeColour(
                        buffer[id]
                            .Info.m_investmentAI.CalculateChangeRate(
                                (byte)id,
                                ref buffer[id],
                                TickerFeed.PanelChangeRateWindow
                            )
                    )
                );
                view.Draw(range, colour, StockSymbols.For(buffer[id].Info));

                // UIGraph draws its curves and labels above its children, so the overlay can't
                // cover them: clear the curve vanilla just added (it re-adds it on its next
                // update, so the flag off / mod disabled shows the vanilla chart again).
                graph.Clear();
            }
            catch (Exception ex)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    ModLog.Warn("share chart: overlay failed, vanilla chart kept: " + ex.Message);
                }

                Remove(graph);
            }
        }

        /// <summary>Per frame (Stock Monitor host): hover on whichever share chart is under the mouse.</summary>
        public static void UpdateHover()
        {
            if (Views.Count == 0)
            {
                return;
            }

            try
            {
                Vector2 mouse = FoPointer.Position;
                foreach (KeyValuePair<UIGraph, ShareChartView> kv in Views)
                {
                    if (kv.Key == null || !kv.Key.isVisible)
                    {
                        kv.Value.HideHover();
                        continue;
                    }

                    kv.Value.Hover(mouse);
                }
            }
            catch (Exception ex)
            {
                if (!hoverFailureLogged)
                {
                    hoverFailureLogged = true;
                    ModLog.Warn("share chart: hover failed: " + ex);
                }
            }
        }

        /// <summary>Options flag off, hot reload, mod disabled: back to the vanilla charts.</summary>
        public static void RemoveAll()
        {
            foreach (ShareChartView v in Views.Values)
            {
                v.Destroy();
            }

            Views.Clear();
            var stale = new List<GameObject>();
            foreach (UIComponent c in UnityEngine.Object.FindObjectsOfType<UIComponent>())
            {
                if (c != null && c.name == ShareChartView.Prefix && c.parent is UIGraph)
                {
                    stale.Add(c.gameObject);
                }
            }

            foreach (GameObject go in stale)
            {
                UnityEngine.Object.Destroy(go);
            }
        }

        private static void Remove(UIGraph graph)
        {
            ShareChartView view;
            if (Views.TryGetValue(graph, out view))
            {
                view.Destroy();
                Views.Remove(graph);
            }
        }

        /// <summary>An overlay left on this graph by an older assembly (hot reload).</summary>
        private static void RemoveStale(UIGraph graph)
        {
            UiCleanup.RemoveChildrenWhere(graph, c => c.name == ShareChartView.Prefix);
        }

        /// <summary>Says once which game member is missing; the vanilla chart is then kept.</summary>
        private static FieldInfo Need(FieldInfo f, string what)
        {
            if (f == null)
            {
                ModLog.Warn("share chart: " + what + " not found; vanilla chart kept");
            }

            return f;
        }
    }
}
