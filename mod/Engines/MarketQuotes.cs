using System.Collections.Generic; // Dictionary, List
using FinanceOverhaul.Core; // MonitorQuote, TradeOrder

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyManager,
//   Investment, InvestmentAI, InvestmentInfo

namespace FinanceOverhaul
{
    /// <summary>
    /// Every listed stock as the mod's lists show it (the Stock Monitor and Economy →
    /// Investments): symbol, name, price, the game's change rate, 30 daily prices, what the city
    /// holds and paid, open orders, whether it is a local company, and, for a locked stock, the
    /// game's requirement. Read from the game each time; nothing kept.
    /// </summary>
    internal static class MarketQuotes
    {
        /// <param name="withLocked">Also list stocks whose requirement is not met yet.</param>
        public static List<MonitorQuote> Read(bool withLocked)
        {
            var quotes = new List<MonitorQuote>();
            var orders = new Dictionary<string, int>();
            foreach (TradeOrder o in OrderDesk.Snapshot())
            {
                int n;
                orders.TryGetValue(o.InvestmentKey, out n);
                orders[o.InvestmentKey] = n + 1;
            }

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                InvestmentAI ai = info == null ? null : info.m_investmentAI;
                if (ai == null)
                {
                    continue;
                }

                bool available = ai.IsAvailable((byte)i, ref buffer[i]);
                if (!available && !withLocked)
                {
                    continue;
                }

                var history = new float[Investment.kPastValueCount];
                buffer[i].FillGraphValues(history);
                history = CompanyListing.ChartDays(ai, history);
                int count;
                orders.TryGetValue(info.name, out count);
                int company;
                var q = new MonitorQuote
                {
                    Id = i,
                    Prefab = info.name,
                    Symbol = StockSymbols.For(info),
                    Name = ai.GetLocalizedName(),
                    Price = buffer[i].value,
                    ChangeRate = ai.CalculateChangeRate(
                        (byte)i,
                        ref buffer[i],
                        TickerFeed.PanelChangeRateWindow
                    ),
                    History = history,
                    Owned = ai.OwnedCount(ref buffer[i]),
                    Available = available ? ai.AvailableForBuyingCount(ref buffer[i]) : 0u,
                    OpenOrders = count,
                    Spent = buffer[i].m_playerSpentMoney,
                    Local = CompanyListing.IsCompany(ai, out company),
                    Locked = !available,
                    Coin = ai.m_unit == InvestmentAI.Unit.Coin,
                };
                if (!available)
                {
                    string text;
                    q.UnlockProgress = ai.GetUnlockProgress(out text);
                    q.UnlockText = text;
                }

                quotes.Add(q);
            }

            return quotes;
        }
    }
}
