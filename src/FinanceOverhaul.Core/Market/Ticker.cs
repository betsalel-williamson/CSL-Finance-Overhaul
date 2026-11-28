using System; // Math, MidpointRounding, StringComparer, StringComparison, StringSplitOptions
using System.Collections.Generic; // Dictionary, IList, List
using System.Globalization; // CultureInfo
using System.Text; // StringBuilder

namespace FinanceOverhaul.Core
{
    /// <summary>How the Stock Exchange LED ticker shows each stock's change (Options).</summary>
    public enum TickerDisplay
    {
        /// <summary>"DS +3.22%": the game's change rate with a % sign (default).</summary>
        ChangePercent = 0,

        /// <summary>"DS ₡951 +₡30": price and the same change in ₡, like an exchange tape.</summary>
        PriceAndChange = 1,

        /// <summary>
        /// As the change pills show it (default): % while they show %, price and ₡ change
        /// after a click on any pill flips them to points (<see cref="ModSettings.TickerShowsNow"/>).
        /// </summary>
        FollowPills = 2,
    }

    /// <summary>
    /// Ticker symbols for the Stock Exchange boards. Uses the symbols the vanilla
    /// facade artwork already shows where an investment clearly matches, plus new
    /// ones for names the artwork never covered.
    /// </summary>
    public static class TickerSymbols
    {
        private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "Traffic Jelly Logistics", "TJL" },
            { "The Very Legit Company", "TVLC" },
            { "Deathcare Service Fund", "DS" },
            { "Healthcare Service Fund", "HS" },
            { "Farming Industry", "FaI" },
            { "Forestry Industry", "FoI" },
            { "Ore Industry", "OrI" },
            { "Oil Industry", "Oil" },
            { "ChirpAir", "CAi" },
            { "Chirpy Cruises", "CCr" },
            { "Chirp Coin", "CC" },
            { "Generic Industry", "GeI" },
            { "Greasy Gasoline Inc.", "GGI" },
            { CityIndexBasket.IndexName, "IDX" },
            { TestStock.DisplayName, "TST" },
        };

        /// <summary>
        /// Known names map to fixed symbols. Otherwise: several words → initials (up to
        /// 4); one CamelCase word → first letter + first two of the next part
        /// (ChirpAir → CAi); one plain word → its first three letters.
        /// </summary>
        public static string For(string name)
        {
            if (name == null)
            {
                return "?";
            }

            string trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                return "?";
            }

            string known;
            if (Known.TryGetValue(trimmed, out known))
            {
                return known;
            }

            // Symbols use letters and digits only: punctuation such as "(dev)" or "Joe's" is
            // dropped, and a word with neither counts for nothing.
            var kept = new List<string>();
            foreach (
                string raw in trimmed.Split(
                    new[] { ' ', '\t', '-', '_', '.', ',', '&' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                var clean = new StringBuilder(raw.Length);
                foreach (char c in raw)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        clean.Append(c);
                    }
                }

                if (clean.Length > 0)
                {
                    kept.Add(clean.ToString());
                }
            }

            if (kept.Count == 0)
            {
                return "?";
            }

            if (kept.Count >= 2)
            {
                var sb = new StringBuilder(4);
                for (int i = 0; i < kept.Count && sb.Length < 4; i++)
                {
                    sb.Append(char.ToUpperInvariant(kept[i][0]));
                }

                return sb.ToString();
            }

            string word = kept[0];
            for (int i = 1; i < word.Length; i++)
            {
                if (char.IsUpper(word[i]))
                {
                    string rest = word.Substring(i, Math.Min(2, word.Length - i));
                    return char.ToUpperInvariant(word[0]) + rest;
                }
            }

            string head = word.Substring(0, Math.Min(3, word.Length));
            return char.ToUpperInvariant(head[0]) + head.Substring(1);
        }
    }

    /// <summary>
    /// Formats one ticker line in the facade style, <c>SYM ( +2.67 )</c>, rounding to two
    /// decimals like the Economy panel's Change rate. Never prints NaN: non-finite
    /// values show as <c>( -- )</c>.
    /// </summary>
    public static class TickerLine
    {
        public const string Separator = "  ";

        public static string Format(IList<string> symbols, IList<float> changeRates)
        {
            if (symbols == null || changeRates == null)
            {
                return string.Empty;
            }

            int n = Math.Min(symbols.Count, changeRates.Count);
            var sb = new StringBuilder(n * 14);
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    sb.Append(Separator);
                }

                sb.Append(symbols[i]).Append(" ( ").Append(FormatRate(changeRates[i])).Append(" )");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Sorts entries alphabetically by symbol (case-insensitive, stable), keeping each
        /// rate paired with its symbol. No-op for null or mismatched lists.
        /// </summary>
        public static void SortBySymbol(IList<string> symbols, IList<float> changeRates)
        {
            if (symbols == null || changeRates == null || symbols.Count != changeRates.Count)
            {
                return;
            }

            // Insertion sort: tiny lists, and stable for equal symbols.
            for (int i = 1; i < symbols.Count; i++)
            {
                string symbol = symbols[i];
                float rate = changeRates[i];
                int j = i - 1;
                while (
                    j >= 0
                    && string.Compare(symbols[j], symbol, StringComparison.OrdinalIgnoreCase) > 0
                )
                {
                    symbols[j + 1] = symbols[j];
                    changeRates[j + 1] = changeRates[j];
                    j--;
                }

                symbols[j + 1] = symbol;
                changeRates[j + 1] = rate;
            }
        }

        // Figma tokens color/ticker/* (sampled from the vanilla facade boards).
        public const string SymbolHex = "#EFB058";
        public const string GainHex = "#4EF465";
        public const string LossHex = "#E9424F";
        public const string UnknownHex = "#9FB0C0";

        // Figma token color/ticker/flat: unchanged (0.00) is neutral, not green.
        public const string FlatHex = "#9FB0C0";

        /// <summary>
        /// Splits entries into <paramref name="rows"/> board rows (as even as possible,
        /// earlier rows fuller) using Unity rich-text color tags from the ticker tokens.
        /// Always returns at least one row.
        /// </summary>
        public static string[] FormatRichRows(
            IList<string> symbols,
            IList<float> changeRates,
            int rows
        )
        {
            rows = Math.Max(1, rows);
            var result = new string[rows];
            for (int r = 0; r < rows; r++)
            {
                result[r] = string.Empty;
            }

            if (symbols == null || changeRates == null)
            {
                return result;
            }

            int n = Math.Min(symbols.Count, changeRates.Count);
            int perRow = (n + rows - 1) / rows;
            if (perRow == 0)
            {
                return result;
            }

            var sb = new StringBuilder(perRow * 48);
            for (int r = 0; r < rows; r++)
            {
                sb.Length = 0;
                for (int i = r * perRow; i < Math.Min(n, (r + 1) * perRow); i++)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(Separator);
                    }

                    float rate = changeRates[i];
                    byte tone = LedText.RateColour(rate);
                    string color =
                        tone == LedText.Unknown ? UnknownHex
                        : tone == LedText.Flat ? FlatHex
                        : tone == LedText.Loss ? LossHex
                        : GainHex;
                    sb.Append("<color=")
                        .Append(SymbolHex)
                        .Append('>')
                        .Append(symbols[i])
                        .Append("</color> <color=")
                        .Append(color)
                        .Append(">( ")
                        .Append(FormatRate(rate))
                        .Append(" )</color>");
                }

                result[r] = sb.ToString();
            }

            return result;
        }

        public static string FormatRate(float rate)
        {
            if (float.IsNaN(rate) || float.IsInfinity(rate))
            {
                return "--";
            }

            double rounded = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
            if (rounded == 0d)
            {
                return "0.00";
            }

            return (rounded > 0 ? "+" : string.Empty)
                + rounded.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
