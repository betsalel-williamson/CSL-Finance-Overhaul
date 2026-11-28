using System; // Exception
using System.Collections.Generic; // KeyValuePair, List
using ColossalFramework.UI; // UIComponent, UILabel, UIOrientation, UIPanel, UIScrollablePanel
using FinanceOverhaul.Core; // StockChirp, StockChirps, Strings
using UnityEngine; // Mathf, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyManager,
//   Investment, InvestmentMessage, MessageBase, MessageManager

namespace FinanceOverhaul
{
    /// <summary>
    /// The Invest dialog's Chirps tab, after Buy | Sell: what Chirper said about the stock
    /// lately (<see cref="StockChirps"/>). Choosing it hides the trade (the side stays as it
    /// was) and shows the chirps in the same room, so the dialog keeps its size; Buy or Sell
    /// brings the trade back.
    /// </summary>
    public static partial class OrderTicket
    {
        public const string ChirpsName = "FinanceOverhaulOrderChirps";
        private const float ChirpGap = 4f;

        private static UIPanel chirps;
        private static UIScrollablePanel chirpList;
        private static bool chirpsShown;
        private static readonly List<UIComponent> hiddenForChirps = new List<UIComponent>();

        private static void BuildChirps()
        {
            chirps = dark.AddUIComponent<UIPanel>();
            chirps.name = ChirpsName;
            chirps.size = new Vector2(dark.width, 160f);
            chirps.isVisible = false;
            chirps.tooltip = Strings.Get("chirps.tip");
            chirpList = chirps.AddUIComponent<UIScrollablePanel>();
            chirpList.name = ChirpsName + "List";
            chirpList.relativePosition = new Vector3(26f, 6f);
            chirpList.clipChildren = true;
            chirpList.autoLayout = false;
            chirpList.scrollWheelDirection = UIOrientation.Vertical;
            chirpList.scrollWheelAmount = 26;
            chirpList.builtinKeyNavigation = true;
            chirps.zOrder = sides.zOrder + 1;
        }

        /// <summary>Shows the stock's chirps in place of the trade, or the trade again.</summary>
        private static void ShowChirps(bool on)
        {
            if (dark == null || chirps == null)
            {
                return;
            }

            if (!on)
            {
                if (!chirpsShown)
                {
                    return;
                }

                chirpsShown = false;
                chirps.isVisible = false;
                foreach (UIComponent c in hiddenForChirps)
                {
                    if (c != null)
                    {
                        c.isVisible = true;
                    }
                }

                hiddenForChirps.Clear();
                StyleTabs();
                Relayout();
                return;
            }

            if (!chirpsShown)
            {
                // The trade folds away; the chirps take its room so the dialog keeps its size.
                float room = 0f;
                foreach (UIComponent c in dark.components)
                {
                    if (c == sides || c == chirps || !c.isVisible)
                    {
                        continue;
                    }

                    room += c.height;
                    hiddenForChirps.Add(c);
                    c.isVisible = false;
                }

                chirpsShown = true;
                chirps.height = Mathf.Max(120f, room);
                chirpList.size = new Vector2(dark.width - 52f, chirps.height - 12f);
                chirps.isVisible = true;
            }

            FillChirps();
            StyleTabs();
        }

        private static void FillChirps()
        {
            foreach (UIComponent old in new List<UIComponent>(chirpList.components))
            {
                UnityEngine.Object.Destroy(old.gameObject);
            }

            // Laid out from the top again: back to it, so the rows and the scroll agree.
            chirpList.scrollPosition = Vector2.zero;

            byte id = owner == null ? (byte)0 : (byte)IdField.GetValue(owner);
            Investment data = EconomyManager.instance.m_investments.m_buffer[id];
            string symbol = data.Info == null ? null : StockSymbols.For(data.Info);
            List<StockChirp> list = StockChirps.For(
                symbol,
                ChirpNotices.Log.Snapshot(),
                GameChirps(id),
                OrderDesk.Today
            );
            float width = chirpList.width;
            float y = 0f;
            if (list.Count == 0)
            {
                UILabel none = FoText.Label(
                    chirpList,
                    ChirpsName + "None",
                    "",
                    0.8f,
                    FoTheme.TextMuted
                );
                FoText.Fit(none, Strings.Get("chirps.none"), 0.8f, 0.7f, width, 15f);
                none.relativePosition = Vector3.zero;
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                StockChirp c = list[i];
                UIPanel row = FoRow.Add(
                    chirpList,
                    ChirpsName + "-" + i,
                    new Vector2(width, 40f),
                    new Vector3(0f, y),
                    null
                );
                UILabel sender = FoText.Label(
                    row,
                    row.name + "Sender",
                    c.Sender,
                    0.75f,
                    FoTheme.Accent
                );
                sender.relativePosition = new Vector3(8f, 4f);
                UILabel when = FoText.Label(
                    row,
                    row.name + "When",
                    c.When,
                    0.65f,
                    FoTheme.TextMuted
                );
                when.relativePosition = new Vector3(width - 8f - when.width, 5f);
                UILabel text = FoText.Label(row, row.name + "Text", "", 0.75f, FoTheme.GameText);
                float h = FoText.Fit(text, c.Text, 0.75f, 0.65f, width - 16f, 14f);
                text.relativePosition = new Vector3(8f, 20f);
                row.height = 20f + h + 6f;
                y += row.height + ChirpGap;
            }
        }

        /// <summary>The game's own chirps about the stock still in its recent messages, newest first.</summary>
        private static List<KeyValuePair<string, string>> GameChirps(byte id)
        {
            var found = new List<KeyValuePair<string, string>>();
            try
            {
                var recent =
                    GameInternals.Chirper.RecentMessages == null
                        ? null
                        : GameInternals.Chirper.RecentMessages.GetValue(MessageManager.instance)
                            as MessageBase[];
                if (recent == null)
                {
                    return found;
                }

                for (int i = recent.Length - 1; i >= 0; i--)
                {
                    var m = recent[i] as InvestmentMessage;
                    if (m != null && m.m_investmentID == id)
                    {
                        found.Add(new KeyValuePair<string, string>(m.GetSenderName(), m.GetText()));
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("order ticket: chirps: " + ex.Message);
            }

            return found;
        }
    }
}
