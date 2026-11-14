#if HAS_CITIES
using System;
using System.Collections.Generic;
using FinanceOverhaul.Market;

namespace FinanceOverhaul
{
    /// <summary>
    /// One symbol map for every loaded stock prefab (LED boards, Stock Monitor, portfolio
    /// rows), built by <see cref="SymbolBook"/> from all prefabs — listed or not — so a symbol
    /// never depends on which stocks happen to be listed today. Rebuilt when the prefab count
    /// changes (a listing added by this mod or another one). Display only.
    /// </summary>
    public static class StockSymbols
    {
        private static Dictionary<string, string> map;
        private static int builtFor = -1;

        public static string For(InvestmentInfo info)
        {
            if (info == null)
            {
                return "?";
            }

            try
            {
                int count = PrefabCollection<InvestmentInfo>.LoadedCount();
                if (map == null || builtFor != count)
                {
                    Build(count);
                }

                string symbol;
                if (map.TryGetValue(info.name, out symbol))
                {
                    return symbol;
                }
            }
            catch
            {
                // fall through to the display-name rule
            }

            return TickerSymbols.For(Display(info));
        }

        /// <summary>Dev: logs every stock prefab with its display name, price model and symbol.</summary>
        public static void Dump()
        {
            int count = PrefabCollection<InvestmentInfo>.LoadedCount();
            Build(count);
            for (int i = 0; i < count; i++)
            {
                InvestmentInfo info = PrefabCollection<InvestmentInfo>.GetLoaded((uint)i);
                if (info == null)
                {
                    continue;
                }

                string symbol;
                map.TryGetValue(info.name, out symbol);
                ModLog.Info(
                    "symbols: #"
                        + i
                        + " prefab='"
                        + info.name
                        + "' display='"
                        + Display(info)
                        + "' ai="
                        + (info.m_investmentAI == null ? "-" : info.m_investmentAI.GetType().Name)
                        + " symbol="
                        + symbol
                        + (SymbolBook.Fixed.ContainsKey(info.name) ? " (fixed)" : " (derived)")
                );
            }

            List<string> clashes = SymbolBook.Clashes(map);
            ModLog.Info(
                "symbols: "
                    + map.Count
                    + " prefab(s), "
                    + (
                        clashes.Count == 0
                            ? "no clashes"
                            : "CLASHES " + string.Join("; ", clashes.ToArray())
                    )
            );
        }

        /// <summary>Level unload / hot reload: the next city builds its own map.</summary>
        public static void Reset()
        {
            map = null;
            builtFor = -1;
        }

        private static void Build(int count)
        {
            var stocks = new List<KeyValuePair<string, string>>(count);
            for (int i = 0; i < count; i++)
            {
                InvestmentInfo info = PrefabCollection<InvestmentInfo>.GetLoaded((uint)i);
                if (info != null)
                {
                    stocks.Add(new KeyValuePair<string, string>(info.name, Display(info)));
                }
            }

            map = SymbolBook.Assign(stocks);
            builtFor = count;
        }

        private static string Display(InvestmentInfo info)
        {
            try
            {
                return info.m_investmentAI == null
                    ? info.name
                    : info.m_investmentAI.GetLocalizedName();
            }
            catch
            {
                return info.name;
            }
        }
    }
}
#endif
