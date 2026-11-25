using System; // Math, StringComparer, StringComparison
using System.Collections.Generic; // Dictionary, IList, List
using System.Globalization; // CultureInfo, NumberStyles
using System.Text; // StringBuilder

namespace FinanceOverhaul.Core
{
    /// <summary>A preset the player saved: a name and the player's Local IPOs values.</summary>
    public sealed class IpoUserPreset
    {
        public string Name;

        /// <summary>Stored values by settings key; a key missing here is its default.</summary>
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
    }

    /// <summary>
    /// Whole-set presets for the player's Local IPOs values, as Trackpad Camera Control does its
    /// feel presets: three built in (less likely, default, more likely; never overwritten), any
    /// number the player saves by name, and "New Preset" for values that match none of them.
    /// Which one is in use is worked out from the values, so it is never out of date. The
    /// finer workings (dev builds) are not part of a preset.
    /// </summary>
    public static class IpoPresets
    {
        /// <summary>The longest name a saved preset may have.</summary>
        public const int MaxNameLength = 40;

        private static readonly TuningPreset[] BuiltIns =
        {
            TuningPreset.LessLikely,
            TuningPreset.Default,
            TuningPreset.MoreLikely,
        };

        private static readonly TuningGroup[] PlayerGroups =
        {
            TuningGroup.Listing,
            TuningGroup.Candidates,
            TuningGroup.Prices,
            TuningGroup.BuyoutsAndClosing,
        };

        /// <summary>A built-in preset's name as the player reads it ("More likely").</summary>
        public static string BuiltInName(TuningPreset preset)
        {
            return Strings.Get("options.ipo.preset.name." + preset);
        }

        /// <summary>The name shown while the values match no preset.</summary>
        public static string NewPresetName
        {
            get { return Strings.Get("options.ipo.preset.new"); }
        }

        /// <summary>A name that a built-in preset or "New Preset" already has (case ignored).</summary>
        public static bool IsReservedName(string name)
        {
            string n = Clean(name);
            foreach (TuningPreset p in BuiltIns)
            {
                if (string.Equals(n, BuiltInName(p), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return string.Equals(n, NewPresetName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sets every player value to a built-in preset (the finer workings stay).</summary>
        public static void Apply(IpoRules rules, TuningPreset preset)
        {
            foreach (TuningGroup g in PlayerGroups)
            {
                IpoTuning.Apply(rules, g, preset);
            }
        }

        /// <summary>Sets every player value to a saved preset (a value it lacks: the default).</summary>
        public static void Apply(IpoRules rules, IpoUserPreset preset)
        {
            foreach (IpoTuning t in IpoTuning.All)
            {
                if (t.Audience != TuningAudience.Player)
                {
                    continue;
                }

                int v;
                t.Set(
                    rules,
                    preset.Values.TryGetValue(t.Key, out v)
                        ? Math.Max(t.Min, Math.Min(t.Max, v))
                        : t.Default
                );
            }
        }

        /// <summary>The player's values now, as a preset with this name.</summary>
        public static IpoUserPreset Snapshot(IpoRules rules, string name)
        {
            var preset = new IpoUserPreset { Name = Clean(name) };
            foreach (IpoTuning t in IpoTuning.All)
            {
                if (t.Audience == TuningAudience.Player)
                {
                    preset.Values[t.Key] = t.Get(rules);
                }
            }

            return preset;
        }

        /// <summary>
        /// The preset the values are now: a built-in's name, a saved one's, or "New Preset".
        /// </summary>
        public static string Identify(IpoRules rules, IList<IpoUserPreset> saved)
        {
            foreach (TuningPreset p in BuiltIns)
            {
                bool all = true;
                foreach (TuningGroup g in PlayerGroups)
                {
                    all &= IpoTuning.Matches(rules, g, p);
                }

                if (all)
                {
                    return BuiltInName(p);
                }
            }

            foreach (IpoUserPreset s in saved ?? new List<IpoUserPreset>())
            {
                if (Same(rules, s))
                {
                    return s.Name;
                }
            }

            return NewPresetName;
        }

        /// <summary>The dropdown's items: the built-ins, the saved ones, and "New Preset" when in use.</summary>
        public static List<string> Names(IpoRules rules, IList<IpoUserPreset> saved)
        {
            var names = new List<string>();
            foreach (TuningPreset p in BuiltIns)
            {
                names.Add(BuiltInName(p));
            }

            foreach (IpoUserPreset s in saved ?? new List<IpoUserPreset>())
            {
                names.Add(s.Name);
            }

            if (Identify(rules, saved) == NewPresetName)
            {
                names.Add(NewPresetName);
            }

            return names;
        }

        /// <summary>
        /// Applies the dropdown's item <paramref name="name"/>: a built-in or a saved preset.
        /// False for "New Preset" or an unknown name (nothing changes).
        /// </summary>
        public static bool ApplyNamed(IpoRules rules, IList<IpoUserPreset> saved, string name)
        {
            foreach (TuningPreset p in BuiltIns)
            {
                if (name == BuiltInName(p))
                {
                    Apply(rules, p);
                    return true;
                }
            }

            IpoUserPreset s = Find(saved, name);
            if (s == null)
            {
                return false;
            }

            Apply(rules, s);
            return true;
        }

        /// <summary>
        /// Saves the values now under <paramref name="name"/>, replacing a saved preset of that
        /// name. False for an empty or reserved name.
        /// </summary>
        public static bool Save(IList<IpoUserPreset> saved, IpoRules rules, string name)
        {
            string n = Clean(name);
            if (n.Length == 0 || IsReservedName(n))
            {
                return false;
            }

            IpoUserPreset old = Find(saved, n);
            if (old != null)
            {
                saved.Remove(old);
            }

            saved.Add(Snapshot(rules, n));
            return true;
        }

        /// <summary>Removes a saved preset; false if there is none of that name.</summary>
        public static bool Delete(IList<IpoUserPreset> saved, string name)
        {
            IpoUserPreset s = Find(saved, name);
            return s != null && saved.Remove(s);
        }

        /// <summary>A name as kept: trimmed, one line, no separators, at most <see cref="MaxNameLength"/>.</summary>
        public static string Clean(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in (name ?? string.Empty).Trim())
            {
                if (c != '|' && c != ';' && c != '=' && c != '\n' && c != '\r')
                {
                    sb.Append(c);
                }
            }

            string n = sb.ToString().Trim();
            return n.Length > MaxNameLength ? n.Substring(0, MaxNameLength).Trim() : n;
        }

        /// <summary>A name the Save as… dialog suggests: "My preset", "My preset 2", …</summary>
        public static string SuggestName(IList<IpoUserPreset> saved)
        {
            string stem = Strings.Get("options.ipo.preset.suggest");
            string name = stem;
            for (int i = 2; Find(saved, name) != null; i++)
            {
                name = stem + " " + i.ToString(CultureInfo.InvariantCulture);
            }

            return name;
        }

        /// <summary>A saved preset as one settings value: <c>Name|key=v;key=v</c>.</summary>
        public static string Format(IpoUserPreset preset)
        {
            var sb = new StringBuilder(Clean(preset.Name)).Append('|');
            bool first = true;
            var keys = new List<string>(preset.Values.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                sb.Append(first ? string.Empty : ";")
                    .Append(key)
                    .Append('=')
                    .Append(preset.Values[key].ToString(CultureInfo.InvariantCulture));
                first = false;
            }

            return sb.ToString();
        }

        /// <summary>Reads a value written by <see cref="Format"/>; null if it has no name.</summary>
        public static IpoUserPreset Parse(string value)
        {
            int bar = (value ?? string.Empty).IndexOf('|');
            string name = Clean(bar < 0 ? value : value.Substring(0, bar));
            if (name.Length == 0 || IsReservedName(name))
            {
                return null;
            }

            var preset = new IpoUserPreset { Name = name };
            if (bar < 0)
            {
                return preset;
            }

            foreach (string pair in value.Substring(bar + 1).Split(';'))
            {
                int eq = pair.IndexOf('=');
                int v;
                if (
                    eq > 0
                    && IpoTuning.Find(pair.Substring(0, eq)) != null
                    && int.TryParse(
                        pair.Substring(eq + 1),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out v
                    )
                )
                {
                    preset.Values[IpoTuning.Find(pair.Substring(0, eq)).Key] = v;
                }
            }

            return preset;
        }

        private static bool Same(IpoRules rules, IpoUserPreset preset)
        {
            foreach (IpoTuning t in IpoTuning.All)
            {
                if (t.Audience != TuningAudience.Player)
                {
                    continue;
                }

                int v;
                int want = preset.Values.TryGetValue(t.Key, out v) ? v : t.Default;
                if (t.Get(rules) != want)
                {
                    return false;
                }
            }

            return true;
        }

        private static IpoUserPreset Find(IList<IpoUserPreset> saved, string name)
        {
            string n = Clean(name);
            foreach (IpoUserPreset s in saved ?? new List<IpoUserPreset>())
            {
                if (string.Equals(s.Name, n, StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }

            return null;
        }
    }
}
