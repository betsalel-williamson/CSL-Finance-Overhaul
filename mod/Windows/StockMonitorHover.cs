using ColossalFramework.UI; // UIComponent, UILabel, UIPanel
using FinanceOverhaul.Core; // Sparkline, StockDetail, StockMonitor
using UnityEngine; // Mathf, RectOffset, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// Hover (Figma band 10, pin 7): over any row sparkline or the detail chart, a crosshair,
    /// a dot on the line and a bubble with that day's price ("Oil · 8 days ago · ₡925"), like
    /// City Vitals' value bubble. Hit-testing runs only while the mouse is over the window.
    /// </summary>
    public sealed partial class StockMonitorHost
    {
        private UILabel hoverTip;
        private UIPanel hoverLine;
        private UIPanel hoverDot;

        private void UpdateHover()
        {
            if (
                window == null
                || !window.isVisible
                || (!FoPointer.Test.HasValue && !window.containsMouse)
            )
            {
                HideHover();
                return;
            }

            Vector2 mouse = FoPointer.Position;
            if (detailId >= 0 && detail != null && detail.isVisible)
            {
                if (!HoverOver(dChart, mouse, dRangeShown, dSymbolShown, 4, ChartScale))
                {
                    HideHover();
                }

                return;
            }

            foreach (Row r in rows)
            {
                if (
                    r.Panel.isVisible
                    && r.History != null
                    && InsideList(r.Spark)
                    && HoverOver(r.Spark, mouse, r.History, r.Sym, SparkScale + 1, SparkScale)
                )
                {
                    return;
                }
            }

            HideHover();
        }

        /// <summary>Shows the hover for this chart when the mouse is over it.</summary>
        private bool HoverOver(
            UIComponent chart,
            Vector2 mouse,
            float[] values,
            string symbol,
            int thickness,
            int scale
        )
        {
            if (chart == null || values == null || values.Length == 0)
            {
                return false;
            }

            Vector3 p = chart.absolutePosition;
            float lx = mouse.x - p.x,
                ly = mouse.y - p.y;
            if (lx < 0f || ly < 0f || lx > chart.width || ly > chart.height)
            {
                return false;
            }

            int w = (int)chart.width * scale,
                h = (int)chart.height * scale;
            int i = Sparkline.IndexAt(lx * scale, w, values.Length);
            float x = Sparkline.ColumnOf(i, w, values.Length) / scale;
            float lo = StockDetail.Low(values),
                hi = StockDetail.High(values);
            float y = Sparkline.RowOf(values[i], lo, hi, h, thickness) / (float)scale;

            EnsureHover();
            Vector3 origin = window.absolutePosition;
            Vector3 at = p - origin;
            hoverLine.size = new Vector2(1f, chart.height);
            hoverLine.relativePosition = new Vector3(at.x + x, at.y);
            hoverDot.relativePosition = new Vector3(at.x + x - 3f, at.y + y - 3f);
            hoverTip.text = StockMonitor.HoverText(symbol, i, values.Length, values[i]);
            float tx = Mathf.Clamp(at.x + x - hoverTip.width / 2f, 2f, Width - hoverTip.width - 2f);
            float ty = at.y - hoverTip.height - 2f;
            hoverTip.relativePosition = new Vector3(
                tx,
                ty < TitleHeight ? at.y + chart.height + 2f : ty
            );
            hoverLine.isVisible = hoverDot.isVisible = hoverTip.isVisible = true;
            hoverTip.BringToFront();
            return true;
        }

        private bool InsideList(UIComponent spark)
        {
            float top = list.absolutePosition.y,
                y = spark.absolutePosition.y;
            return y >= top - 1f && y + spark.height <= top + list.height + 1f;
        }

        private void EnsureHover()
        {
            if (hoverTip != null)
            {
                return;
            }

            hoverLine = window.AddUIComponent<UIPanel>();
            hoverLine.name = Prefix + "HoverLine";
            hoverLine.backgroundSprite = "EmptySprite";
            hoverLine.color = FoTheme.HoverLine;
            hoverLine.isInteractive = false;

            hoverDot = window.AddUIComponent<UIPanel>();
            hoverDot.name = Prefix + "HoverDot";
            hoverDot.backgroundSprite = "EmptySprite";
            hoverDot.color = FoTheme.HoverDot;
            hoverDot.size = new Vector2(6f, 6f);
            hoverDot.isInteractive = false;

            hoverTip = FoText.Label(window, Prefix + "HoverTip", "", 0.7f, White);
            hoverTip.backgroundSprite = "GenericPanelWhite";
            hoverTip.color = FoTheme.Tip;
            hoverTip.padding = new RectOffset(6, 6, 3, 2);
            hoverTip.isInteractive = false;
        }

        private void HideHover()
        {
            if (hoverTip != null && hoverTip.isVisible)
            {
                hoverLine.isVisible = hoverDot.isVisible = hoverTip.isVisible = false;
            }
        }
    }
}
