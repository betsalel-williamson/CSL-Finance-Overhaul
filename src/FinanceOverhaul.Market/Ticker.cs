using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FinanceOverhaul.Market
{
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

            string[] words = trimmed.Split(
                new[] { ' ', '\t', '-', '_', '.', ',', '&' },
                StringSplitOptions.RemoveEmptyEntries
            );
            if (words.Length >= 2)
            {
                var sb = new StringBuilder(4);
                for (int i = 0; i < words.Length && sb.Length < 4; i++)
                {
                    sb.Append(char.ToUpperInvariant(words[i][0]));
                }

                return sb.ToString();
            }

            string word = words[0];
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
