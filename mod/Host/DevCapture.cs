#if HAS_CITIES
using System;
using System.Collections.Generic;
using System.IO;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Dev-only live-testing hooks (no OS screen-recording permission needed), active
    /// only in --dev builds (BuildInfo.ShowDevBuildIdentity). Trigger files live next to
    /// the settings file:
    /// <list type="bullet">
    /// <item><c>FinanceOverhaul.capture</c>: saves the rendered frame to
    /// <c>FinanceOverhaul-capture.png</c>.</item>
    /// <item><c>FinanceOverhaul.focus</c>: moves the camera to frame the facade of
    /// Stock Exchange k of n in the loaded city (file text: optional k, default 1; add
    /// "capture" to save a frame once the camera has settled). Does nothing, with a
    /// log line, when the city has no Stock Exchange.</item>
    /// </list>
    /// Each trigger is deleted when handled. Hot-reload safe: lives on the ticker root.
    /// </summary>
    public class DevCapture : MonoBehaviour
    {
        public const string TriggerName = "FinanceOverhaul.capture";
        public const string FocusTriggerName = "FinanceOverhaul.focus";
        public const string OrderTriggerName = "FinanceOverhaul.order";
        public const string OutputName = "FinanceOverhaul-capture.png";

        // Framing: aim a little below the centre board (building frame y 16.9, face z
        // 23.6) so the side boards fit, from 55 m out and 15° above the horizon.
        private const float AimLocalY = 14f;
        private const float AimLocalZ = 23.6f;
        private const float FocusDistance = 55f;
        private const float FocusPitch = 15f;
        private const float SettleSeconds = 3f;

        private float nextCheck;
        private float captureAt = -1f;

        public static string Folder
        {
            get { return Path.GetDirectoryName(Mod.SettingsPath); }
        }

        private void Update()
        {
            if (captureAt >= 0f && Time.unscaledTime >= captureAt)
            {
                captureAt = -1f;
                Capture();
            }

            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 1f;
            try
            {
                string focus = Path.Combine(Folder, FocusTriggerName);
                if (File.Exists(focus))
                {
                    string text = File.ReadAllText(focus);
                    File.Delete(focus);
                    int index;
                    bool capture;
                    FacadeFraming.ParseRequest(text, out index, out capture);
                    if (FocusStockExchange(index) && capture)
                    {
                        captureAt = Time.unscaledTime + SettleSeconds;
                    }
                }

                string orderTrigger = Path.Combine(Folder, OrderTriggerName);
                if (File.Exists(orderTrigger))
                {
                    string text = File.ReadAllText(orderTrigger).Trim();
                    File.Delete(orderTrigger);
                    HandleOrderCommand(text);
                }

                string trigger = Path.Combine(Folder, TriggerName);
                if (File.Exists(trigger))
                {
                    File.Delete(trigger);
                    Capture();
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("dev capture failed: " + ex.Message);
            }
        }

        private static void Capture()
        {
            string output = Path.Combine(Folder, OutputName);
            Application.CaptureScreenshot(output);
            ModLog.Info("dev capture: saved frame to " + output);
        }

        /// <summary>
        /// Dev order commands: "buy SHARES LIMIT INVESTMENT NAME", "cancel ID" or "list".
        /// INVESTMENT NAME is the prefab name (e.g. "Oil Industry") or a ticker symbol.
        /// </summary>
        private static void HandleOrderCommand(string text)
        {
            string[] w = text.Split(new[] { ' ' }, 4, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0)
            {
                return;
            }

            if (w[0] == "dialog" && w.Length >= 2)
            {
                int target = FindInvestment(text.Substring(text.IndexOf(' ') + 1).Trim());
                if (target < 0)
                {
                    ModLog.Warn("orders: dev dialog: investment not found");
                    return;
                }

                BuySellInvestmentPanel panel = BuySellInvestmentPanel.ShowPanel(
                    (byte)target,
                    BuySellInvestmentPanel.Mode.Buy
                );
                if (panel != null)
                {
                    DumpTree(panel.component, 0);
                }

                return;
            }

            if (w[0] == "list")
            {
                foreach (TradeOrder o in OrderDesk.Snapshot())
                {
                    ModLog.Info(
                        "orders: open #"
                            + o.Id
                            + " limit buy "
                            + o.Shares
                            + " "
                            + o.InvestmentKey
                            + " <= "
                            + o.TriggerPrice
                            + " expires day "
                            + o.ExpiresDay
                            + " (today "
                            + OrderDesk.Today
                            + ")"
                    );
                }

                ModLog.Info("orders: " + OrderDesk.Snapshot().Length + " open");
                return;
            }

            int id;
            if (w[0] == "cancel" && w.Length >= 2 && int.TryParse(w[1], out id))
            {
                OrderDesk.Cancel(id);
                return;
            }

            uint shares;
            int limit;
            if (
                w[0] == "buy"
                && w.Length == 4
                && uint.TryParse(w[1], out shares)
                && int.TryParse(w[2], out limit)
            )
            {
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                for (int i = 0; i < buffer.Length; i++)
                {
                    InvestmentInfo info = buffer[i].Info;
                    if ((buffer[i].m_flags & Investment.Flags.Created) == 0 || info == null)
                    {
                        continue;
                    }

                    string symbol = TickerSymbols.For(info.m_investmentAI.GetLocalizedName());
                    if (
                        info.name == w[3]
                        || string.Equals(symbol, w[3], StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        OrderDesk.PlaceLimitBuy((byte)i, shares, limit);
                        return;
                    }
                }

                ModLog.Warn("orders: dev command: no investment named " + w[3]);
                return;
            }

            ModLog.Warn("orders: dev command not understood: " + text);
        }

        private static int FindInvestment(string nameOrSymbol)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                InvestmentInfo info = buffer[i].Info;
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0 || info == null)
                {
                    continue;
                }

                string symbol = TickerSymbols.For(info.m_investmentAI.GetLocalizedName());
                if (
                    info.name == nameOrSymbol
                    || string.Equals(symbol, nameOrSymbol, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Dev: logs a UI subtree (name, type, position, size, visibility).</summary>
        internal static void DumpTree(ColossalFramework.UI.UIComponent c, int depth)
        {
            if (c == null || depth > 6)
            {
                return;
            }

            ModLog.Info(
                "ui: "
                    + new string(' ', depth * 2)
                    + c.name
                    + " <"
                    + c.GetType().Name
                    + "> pos="
                    + c.relativePosition
                    + " size="
                    + c.size
                    + (c.isVisible ? "" : " hidden")
                    + (
                        c is ColossalFramework.UI.UILabel
                            ? " text='" + ((ColossalFramework.UI.UILabel)c).text + "'"
                            : ""
                    )
                    + (
                        c is ColossalFramework.UI.UIButton
                            ? " text='" + ((ColossalFramework.UI.UIButton)c).text + "'"
                            : ""
                    )
            );
            foreach (ColossalFramework.UI.UIComponent child in c.components)
            {
                DumpTree(child, depth + 1);
            }
        }

        /// <summary>Frames Stock Exchange <paramref name="index"/> (1-based) of the city's n, if any.</summary>
        private static bool FocusStockExchange(int index)
        {
            var found = new List<ushort>();
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            for (int i = 1; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Building.Flags.Created) == 0)
                {
                    continue;
                }

                BuildingInfo info = buffer[i].Info;
                if (info != null && info.m_buildingAI is StockExchangeAI)
                {
                    found.Add((ushort)i);
                }
            }

            if (found.Count == 0)
            {
                ModLog.Info("dev focus: this city has no Stock Exchange; camera unchanged");
                return false;
            }

            CameraController camera = ToolsModifierControl.cameraController;
            if (camera == null)
            {
                ModLog.Warn("dev focus: no camera controller");
                return false;
            }

            int k = Math.Min(index, found.Count);
            ushort id = found[k - 1];
            Building b = buffer[id];
            float dx;
            float dz;
            FacadeFraming.LocalToWorld(b.m_angle, 0f, AimLocalZ, out dx, out dz);
            var aim = new Vector3(
                b.m_position.x + dx,
                b.m_position.y + AimLocalY,
                b.m_position.z + dz
            );

            camera.ClearTarget();
            camera.m_targetPosition = aim;
            camera.m_targetAngle = new Vector2(
                FacadeFraming.FacadeYawDegrees(b.m_angle),
                FocusPitch
            );
            camera.m_targetSize = FocusDistance;
            ModLog.Info(
                "dev focus: Stock Exchange "
                    + k
                    + " of "
                    + found.Count
                    + " (building "
                    + id
                    + ") aim="
                    + aim
            );
            return true;
        }
    }
}
#endif
