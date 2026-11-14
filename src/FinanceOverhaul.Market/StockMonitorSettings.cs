using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Stock Monitor choices kept per player in the settings file (never in the city save).
    /// Positions are UI pixels; NaN means "use the default place".
    /// </summary>
    public sealed class StockMonitorSettings
    {
        public const float DefaultOpacity = 0.35f;
        public const float MaxOpacity = 0.9f;

        /// <summary>Temporary feature flag: the toolbar button and window exist at all.</summary>
        public bool Enabled = true;

        /// <summary>The window was open (reopened after a hot reload or a city load).</summary>
        public bool Open;
        public float WindowX = float.NaN;
        public float WindowY = float.NaN;
        public float ButtonX = float.NaN;
        public float ButtonY = float.NaN;

        /// <summary>
        /// Like City Vitals' "Transparent Unhovered": the background sits at <see cref="Opacity"/>
        /// and turns solid (<see cref="HoverOpacity"/>) while the mouse is over the window.
        /// </summary>
        public bool TransparentUnhovered = true;

        public const float HoverOpacity = 0.9f;

        /// <summary>Compact rows: one line per stock, names hidden.</summary>
        public bool CompactRows;

        public bool ShowSparklines = true;

        /// <summary>The change pill shows ₡ vs yesterday instead of the game's % change rate.</summary>
        public bool ChangeAsCurrency;

        /// <summary>Prefab names of stocks the player chose not to show.</summary>
        public readonly HashSet<string> Hidden = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        public MonitorFilter Filter = MonitorFilter.All;
        public MonitorSort Sort = MonitorSort.Change;
        private float opacity = DefaultOpacity;

        /// <summary>
        /// Background darkness, 0 (see-through) to 0.9: always, or only while the mouse is away
        /// when <see cref="TransparentUnhovered"/> is on.
        /// </summary>
        public float Opacity
        {
            get { return opacity; }
            set
            {
                opacity =
                    float.IsNaN(value) || float.IsInfinity(value)
                        ? DefaultOpacity
                        : Math.Max(0f, Math.Min(MaxOpacity, value));
            }
        }

        /// <summary>Target background opacity for the window right now.</summary>
        public float BackgroundOpacity(bool hovered)
        {
            return TransparentUnhovered && hovered ? Math.Max(Opacity, HoverOpacity) : Opacity;
        }

        private const string Prefix = "monitor.";

        /// <summary>Reads one settings line; false when the key isn't a monitor key.</summary>
        public bool TryRead(string key, string value)
        {
            if (key == null || !key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string k = key.Substring(Prefix.Length).ToLowerInvariant();
            switch (k)
            {
                case "enabled":
                    ReadBool(value, ref Enabled);
                    break;
                case "open":
                    ReadBool(value, ref Open);
                    break;
                case "windowx":
                    WindowX = ReadFloat(value);
                    break;
                case "windowy":
                    WindowY = ReadFloat(value);
                    break;
                case "buttonx":
                    ButtonX = ReadFloat(value);
                    break;
                case "buttony":
                    ButtonY = ReadFloat(value);
                    break;
                case "compact":
                    ReadBool(value, ref CompactRows);
                    break;
                case "sparklines":
                    ReadBool(value, ref ShowSparklines);
                    break;
                case "change":
                    ChangeAsCurrency = string.Equals(
                        value,
                        "currency",
                        StringComparison.OrdinalIgnoreCase
                    );
                    break;
                case "hidden":
                    Hidden.Clear();
                    foreach (string part in (value ?? "").Split(';'))
                    {
                        string name = Unescape(part.Trim());
                        if (name.Length > 0)
                        {
                            Hidden.Add(name);
                        }
                    }

                    break;
                case "transparentunhovered":
                    ReadBool(value, ref TransparentUnhovered);
                    break;
                case "opacity":
                    Opacity = ReadFloat(value);
                    break;
                case "filter":
                    ReadEnum(value, ref Filter);
                    break;
                case "sort":
                    ReadEnum(value, ref Sort);
                    break;
            }

            return true; // a monitor key, even if its value was unusable
        }

        public void Write(StringBuilder sb)
        {
            Line(sb, "enabled", Enabled ? "true" : "false");
            Line(sb, "open", Open ? "true" : "false");
            Line(sb, "windowX", Num(WindowX));
            Line(sb, "windowY", Num(WindowY));
            Line(sb, "buttonX", Num(ButtonX));
            Line(sb, "buttonY", Num(ButtonY));
            Line(sb, "opacity", Num(Opacity));
            Line(sb, "transparentUnhovered", TransparentUnhovered ? "true" : "false");
            Line(sb, "compact", CompactRows ? "true" : "false");
            Line(sb, "sparklines", ShowSparklines ? "true" : "false");
            Line(sb, "change", ChangeAsCurrency ? "currency" : "percent");
            var hidden = new List<string>(Hidden);
            hidden.Sort(StringComparer.OrdinalIgnoreCase);
            var parts = new string[hidden.Count];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = Escape(hidden[i]);
            }

            Line(sb, "hidden", string.Join(";", parts));
            Line(sb, "filter", Filter.ToString());
            Line(sb, "sort", Sort.ToString());
        }

        private static string Escape(string s)
        {
            return s.Replace("%", "%25")
                .Replace(";", "%3B")
                .Replace("\n", "%0A")
                .Replace("\r", "%0D");
        }

        private static string Unescape(string s)
        {
            return s.Replace("%0D", "\r")
                .Replace("%0A", "\n")
                .Replace("%3B", ";")
                .Replace("%25", "%");
        }

        private static void Line(StringBuilder sb, string key, string value)
        {
            sb.Append(Prefix).Append(key).Append('=').Append(value).Append('\n');
        }

        private static string Num(float v)
        {
            return float.IsNaN(v) ? "default" : v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static float ReadFloat(string value)
        {
            float v;
            return
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                && !float.IsInfinity(v)
                ? v
                : float.NaN;
        }

        private static void ReadBool(string value, ref bool target)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                target = true;
            }
            else if (
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || value == "0"
            )
            {
                target = false;
            }
        }

        private static void ReadEnum<T>(string value, ref T target)
            where T : struct
        {
            foreach (T candidate in (T[])Enum.GetValues(typeof(T)))
            {
                if (string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    target = candidate;
                    return;
                }
            }
        }
    }
}
