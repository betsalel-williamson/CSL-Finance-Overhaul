using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The mod's wording, looked up by id so it can be translated: English is the built-in
    /// table (<see cref="EnglishStrings"/>), other languages are added as tables and fall
    /// back to English id by id. A whole sentence is one entry with numbered placeholders
    /// ("{0} shares at or below ₡{1}"), never words glued together, because word order differs
    /// between languages. The mod sets <see cref="Language"/> from the game's language.
    /// </summary>
    public static class Strings
    {
        public const string DefaultLanguage = "en";

        private static readonly Dictionary<string, Dictionary<string, string>> Tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                { DefaultLanguage, EnglishStrings.Table },
            };

        private static string language = DefaultLanguage;

        /// <summary>
        /// Two-letter language code as the game names its locales ("en", "de", "zh"…). A
        /// regional code ("pt-BR") is cut to its language; null or empty means English.
        /// </summary>
        public static string Language
        {
            get { return language; }
            set
            {
                string v = (value ?? string.Empty).Trim().ToLowerInvariant();
                int cut = v.IndexOfAny(new[] { '-', '_' });
                if (cut > 0)
                {
                    v = v.Substring(0, cut);
                }

                language = v.Length == 0 ? DefaultLanguage : v;
            }
        }

        /// <summary>Every id the mod uses (the English table).</summary>
        public static ICollection<string> Ids
        {
            get { return EnglishStrings.Table.Keys; }
        }

        /// <summary>Adds or replaces a language's table (translations; tests).</summary>
        public static void AddLanguage(string code, IDictionary<string, string> entries)
        {
            if (string.IsNullOrEmpty(code) || entries == null)
            {
                return;
            }

            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> e in entries)
            {
                table[e.Key] = e.Value;
            }

            Tables[code.Trim().ToLowerInvariant()] = table;
        }

        /// <summary>Removes a language added with <see cref="AddLanguage"/> (English stays).</summary>
        public static void RemoveLanguage(string code)
        {
            if (!string.Equals(code, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                Tables.Remove((code ?? string.Empty).Trim().ToLowerInvariant());
            }
        }

        /// <summary>
        /// The text for an id in the current language, else in English, else the id itself
        /// (so a missing entry is visible, not blank).
        /// </summary>
        public static string Get(string id)
        {
            if (id == null)
            {
                return string.Empty;
            }

            Dictionary<string, string> table;
            string text;
            if (Tables.TryGetValue(language, out table) && table.TryGetValue(id, out text))
            {
                return text;
            }

            return EnglishStrings.Table.TryGetValue(id, out text) ? text : id;
        }

        /// <summary>An entry with its placeholders filled in.</summary>
        public static string Format(string id, params object[] args)
        {
            string text = Get(id);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, text, args);
            }
            catch (FormatException)
            {
                return text; // a translation with a bad placeholder still shows something
            }
        }

        /// <summary>Items as one list in the current language: "IDX, GGI, Oil".</summary>
        public static string List(IList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return string.Empty;
            }

            string text = items[0];
            for (int i = 1; i < items.Count; i++)
            {
                text = Format("join.list", text, items[i]);
            }

            return text;
        }

        /// <summary>
        /// The entry for a count: <paramref name="id"/> plus ".one", ".few", ".many" or
        /// ".other" by the current language's plural rule, falling back to ".other".
        /// </summary>
        public static string Plural(string id, long count, params object[] args)
        {
            string form = PluralForm(language, count);
            string key = id + "." + form;
            Dictionary<string, string> table;
            bool known =
                (Tables.TryGetValue(language, out table) && table.ContainsKey(key))
                || EnglishStrings.Table.ContainsKey(key);
            return Format(known ? key : id + ".other", args);
        }

        /// <summary>
        /// Which plural form a count takes in a language: the game's nine languages. English,
        /// German, Spanish and Portuguese: one / other. French: 0 and 1 are "one". Russian and
        /// Polish: one / few / many. Korean and Chinese: always "other".
        /// </summary>
        public static string PluralForm(string languageCode, long count)
        {
            long n = Math.Abs(count);
            long mod10 = n % 10,
                mod100 = n % 100;
            switch ((languageCode ?? DefaultLanguage).ToLowerInvariant())
            {
                case "ko":
                case "zh":
                    return "other";
                case "fr":
                    return n <= 1 ? "one" : "other";
                case "ru":
                    if (mod10 == 1 && mod100 != 11)
                    {
                        return "one";
                    }

                    return mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)
                        ? "few"
                        : "many";
                case "pl":
                    if (n == 1)
                    {
                        return "one";
                    }

                    return mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)
                        ? "few"
                        : "many";
                default:
                    return n == 1 ? "one" : "other";
            }
        }
    }
}
