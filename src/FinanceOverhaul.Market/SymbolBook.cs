using System;
using System.Collections.Generic;
using System.Text;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Ticker symbols for every listed stock, assigned for the whole list at once so no two
    /// stocks ever share one. Keyed by the investment's prefab name, which is the same in every
    /// language and game version (display names are not). Display only: orders and saves key
    /// on prefab names, so a symbol change can never break a save.
    /// <list type="number">
    /// <item>Prefabs in <see cref="Fixed"/> get their symbol (the game's 13 and the mod's own).</item>
    /// <item>Any other prefab (a future update, another mod) gets a candidate from its display
    /// name (<see cref="TickerSymbols.For"/>); on a clash it is extended with the next letters
    /// of its name, then a digit. Unknown prefabs are handled in prefab-name order, so the
    /// result is the same every session.</item>
    /// </list>
    /// Fixed symbols are reserved even when their stock is not listed, so a stock never takes
    /// a symbol another one will need later.
    /// </summary>
    public static class SymbolBook
    {
        /// <summary>Prefab name → symbol. Symbols must be unique (unit tested).</summary>
        public static readonly Dictionary<string, string> Fixed = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            // The game's 13 (Financial Districts), by prefab name — confirmed with the dev
            // `log-symbols` dump. Symbols follow the vanilla facade artwork where it has one.
            { "Chirp Coin", "CHC" }, // shown as "ChirperCrypto" (the display name changed)
            { "AirplaneInvestment", "CAi" }, // ChirpAir
            { "ShipInvestment", "CCr" }, // Chirpy Cruises
            { "Death Care Sector Investment", "DS" },
            { "Health Care Sector Investment", "HS" },
            { "Fuel Company Investment", "GGI" }, // Greasy Gasoline Inc.
            { "Farming Industry Investment", "FaI" },
            { "Forestry Industry Investment", "FoI" },
            { "Generic Industry Investment", "GeI" },
            { "Oil Industry Investment", "Oil" },
            { "Ore Industry Investment", "OrI" },
            { "TrafficJelly", "TJL" }, // Traffic Jelly Logistics
            { "VeryLegitCompany", "TVLC" }, // The Very Legit Company
            // The mod's own listings.
            { CityIndexBasket.IndexName, "IDX" },
            { TestStock.PrefabName, "TST" },
        };

        private const int MaxLength = 5;

        /// <summary>
        /// Symbols for these stocks (prefab name → display name), as prefab name → symbol.
        /// Null or empty prefab names are skipped; duplicates count once.
        /// </summary>
        public static Dictionary<string, string> Assign(
            IEnumerable<KeyValuePair<string, string>> stocks
        )
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string symbol in Fixed.Values)
            {
                used.Add(symbol);
            }

            var pending = new List<KeyValuePair<string, string>>();
            if (stocks != null)
            {
                foreach (KeyValuePair<string, string> s in stocks)
                {
                    if (string.IsNullOrEmpty(s.Key) || result.ContainsKey(s.Key))
                    {
                        continue;
                    }

                    string symbol;
                    if (Fixed.TryGetValue(s.Key, out symbol))
                    {
                        result[s.Key] = symbol;
                    }
                    else if (!Contains(pending, s.Key))
                    {
                        pending.Add(s);
                    }
                }
            }

            pending.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            foreach (KeyValuePair<string, string> s in pending)
            {
                string name = string.IsNullOrEmpty(s.Value) ? s.Key : s.Value;
                string symbol = Unique(TickerSymbols.For(name), name, used);
                used.Add(symbol);
                result[s.Key] = symbol;
            }

            return result;
        }

        /// <summary>Pairs of stocks that share a symbol (should always be empty).</summary>
        public static List<string> Clashes(IDictionary<string, string> symbols)
        {
            var clashes = new List<string>();
            var owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kv in symbols)
            {
                string other;
                if (owner.TryGetValue(kv.Value, out other))
                {
                    clashes.Add(kv.Value + ": " + other + " / " + kv.Key);
                }
                else
                {
                    owner[kv.Value] = kv.Key;
                }
            }

            return clashes;
        }

        private static bool Contains(List<KeyValuePair<string, string>> list, string key)
        {
            foreach (KeyValuePair<string, string> kv in list)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Unique(string candidate, string name, HashSet<string> used)
        {
            if (string.IsNullOrEmpty(candidate) || candidate == "?")
            {
                candidate = "X";
            }

            if (!used.Contains(candidate))
            {
                return candidate;
            }

            // One more letter from the name (after the ones the candidate already took).
            string letters = Letters(name);
            if (candidate.Length < MaxLength)
            {
                for (int i = 1; i < letters.Length; i++)
                {
                    string next = candidate + char.ToUpperInvariant(letters[i]);
                    if (!used.Contains(next))
                    {
                        return next;
                    }
                }
            }

            for (int n = 2; ; n++)
            {
                string numbered = candidate + n;
                if (!used.Contains(numbered))
                {
                    return numbered;
                }
            }
        }

        private static string Letters(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? string.Empty)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
