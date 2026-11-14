using System;
using System.Text;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Plain <c>key=value</c> settings file so Options survive hot reload and game
    /// restarts. Unknown keys, comments and malformed lines are ignored; anything
    /// missing falls back to the save-safe defaults of <see cref="ProbeSettings"/>.
    /// </summary>
    public static class SettingsFile
    {
        public const string FileName = "FinanceOverhaul.settings.txt";

        private const string ProbeBiasKey = "probeBiasEnabled";
        private const string IndexStockKey = "indexStockEnabled";

        public static ProbeSettings Parse(string text)
        {
            var settings = new ProbeSettings();
            if (string.IsNullOrEmpty(text))
            {
                return settings;
            }

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                bool flag;
                if (!TryParseBool(value, out flag))
                {
                    continue;
                }

                if (string.Equals(key, ProbeBiasKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.ProbeBiasEnabled = flag;
                }
                else if (string.Equals(key, IndexStockKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.IndexStockEnabled = flag;
                }
            }

            return settings;
        }

        public static string Format(ProbeSettings settings)
        {
            settings = settings ?? new ProbeSettings();
            var sb = new StringBuilder();
            sb.Append("# Finance Overhaul settings (written by the mod)\n");
            sb.Append(ProbeBiasKey)
                .Append('=')
                .Append(settings.ProbeBiasEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(IndexStockKey)
                .Append('=')
                .Append(settings.IndexStockEnabled ? "true" : "false")
                .Append('\n');
            return sb.ToString();
        }

        private static bool TryParseBool(string value, out bool result)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                result = true;
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }
    }
}
