using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Hover (Figma band 10, pin 7): over any row sparkline or the detail chart, a crosshair,
    /// a dot on the line and a bubble with that day's price ("Oil · 8 days ago · ₡925"), like
    /// City Vitals' value bubble. Hit-testing runs only while the mouse is over the window.
    /// </summary>
    public sealed partial class StockMonitorHost
    {
        /// <summary>Game UI tests only: a simulated mouse position in UI units (null = real mouse).</summary>
        public static Vector2? TestMouse;

        private UILabel hoverTip;
        private UIPanel hoverLine;
        private UIPanel hoverDot;

        private void UpdateHover()
        {
            if (
                window == null
                || !window.isVisible
                || (!TestMouse.HasValue && !window.containsMouse)
            )
            {
                HideHover();
                return;
            }

            Vector2 mouse = TestMouse ?? MouseInUi();
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

        internal static Vector2 MouseInUi()
        {
            // Screen pixels (y up) → UI units (y down), same scale as the button drag.
            float scale = UIView.GetAView().GetScreenResolution().y / Mathf.Max(1f, Screen.height);
            Vector3 m = Input.mousePosition;
            return new Vector2(m.x * scale, (Screen.height - m.y) * scale);
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
            hoverLine.color = new Color32(255, 255, 255, 150);
            hoverLine.isInteractive = false;

            hoverDot = window.AddUIComponent<UIPanel>();
            hoverDot.name = Prefix + "HoverDot";
            hoverDot.backgroundSprite = "EmptySprite";
            hoverDot.color = new Color32(255, 255, 255, 255);
            hoverDot.size = new Vector2(6f, 6f);
            hoverDot.isInteractive = false;

            hoverTip = Label(window, Prefix + "HoverTip", "", 0.7f, White);
            hoverTip.backgroundSprite = "GenericPanelWhite";
            hoverTip.color = new Color32(9, 14, 17, 235);
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
