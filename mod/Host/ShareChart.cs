#if HAS_CITIES
using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Market;
using UnityEngine;

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
            v.Root.color = new Color32(30, 31, 33, 255); // the game's chart background
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
                UILabel l = StockMonitorHost.Label(
                    v.Root,
                    name + "Axis-" + i,
                    "",
                    labelScale,
                    new Color32(158, 163, 170, 255)
                );
                l.isInteractive = false;
                v.axis.Add(l);
            }

            v.from = StockMonitorHost.Label(
                v.Root,
                name + "From",
                "",
                labelScale,
                new Color32(158, 163, 170, 255)
            );
            v.from.relativePosition = new Vector3(8f, 6f + plot.y + 3f);
            v.from.isInteractive = false;
            v.to = StockMonitorHost.Label(
                v.Root,
                name + "To",
                "today",
                labelScale,
                new Color32(158, 163, 170, 255)
            );
            v.to.isInteractive = false;
            v.to.relativePosition = new Vector3(
                8f + plot.x - 30f * labelScale / 0.7f,
                6f + plot.y + 3f
            );
            return v;
        }

        /// <summary>Draws these daily prices (oldest first); skipped when nothing changed.</summary>
        public void Draw(float[] values, Color32 colour)
        {
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

            from.text = values.Length + " days ago";
            var px = new Color32[w * h];
            var dash = new Color32(colour.r, colour.g, colour.b, 160);
            var gridColour = new Color32(255, 255, 255, 40);
            var clear = new Color32(0, 0, 0, 0);
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
                    if (grid[y] && m != Sparkline.Line)
                    {
                        c = gridColour;
                    }

                    px[(h - 1 - y) * w + x] = c;
                }
            }

            texture.SetPixels32(px);
            texture.Apply();
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
        private static readonly FieldInfo DialogGraph = AccessTools(
            "BuySellInvestmentPanel",
            typeof(BuySellInvestmentPanel),
            "m_graph"
        );
        private static readonly FieldInfo DialogId = AccessTools(
            "BuySellInvestmentPanel",
            typeof(BuySellInvestmentPanel),
            "m_investmentID"
        );
        private static readonly FieldInfo TipGraph = AccessTools(
            "InvestmentTooltip",
            typeof(InvestmentTooltip),
            "m_graph"
        );
        private static readonly FieldInfo TipId = AccessTools(
            "InvestmentTooltip",
            typeof(InvestmentTooltip),
            "m_investmentID"
        );

        /// <summary>UI units the game's chart panel stops short of the graph's bottom edge.</summary>
        private const float PanelBottomInset = 21f;

        private static readonly Dictionary<UIGraph, ShareChartView> Views =
            new Dictionary<UIGraph, ShareChartView>();
        private static bool failureLogged;

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
                float[] range = StockDetail.Range(history, StockDetail.LongRange);
                float? yesterday = StockDetail.Yesterday(history);
                Color32 colour = StockMonitorHost.ColourOf(
                    StockDetail.ChangeColourVsYesterday(buffer[id].value, yesterday)
                );
                view.Draw(range, colour);

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

        private static FieldInfo AccessTools(string owner, Type type, string field)
        {
            FieldInfo f = type.GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );
            if (f == null)
            {
                ModLog.Warn(
                    "share chart: " + owner + "." + field + " not found; vanilla chart kept"
                );
            }

            return f;
        }
    }
}
#endif
