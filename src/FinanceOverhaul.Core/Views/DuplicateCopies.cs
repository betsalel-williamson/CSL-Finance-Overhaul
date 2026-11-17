using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Core
{
    /// <summary>One installed copy of the mod that is switched on.</summary>
    public sealed class ModCopy
    {
        /// <summary>The name it shows in Content Manager, e.g. "Finance Overhaul 0.1.0".</summary>
        public string Name;

        /// <summary>Its assembly version: a newer build is a higher version.</summary>
        public Version Version;

        /// <summary>Subscribed on the Steam Workshop (else a folder under Addons/Mods).</summary>
        public bool Workshop;

        /// <summary>Its folder, to tell copies apart.</summary>
        public string Path;
    }

    /// <summary>
    /// What to do when more than one copy of the mod is switched on (the Workshop copy and a
    /// local one, say). Both would hook the game, so orders, bond payments and the Index would
    /// be handled twice. The player is asked which to keep; the newest is suggested.
    /// </summary>
    public static class DuplicateCopies
    {
        /// <summary>More than one copy switched on.</summary>
        public static bool Conflict(ICollection<ModCopy> enabled)
        {
            return enabled != null && enabled.Count > 1;
        }

        /// <summary>
        /// The copy to suggest keeping: the highest version; on a tie the local copy (the one
        /// the player put there by hand), then the first by path so the answer is stable.
        /// </summary>
        public static ModCopy Suggested(ICollection<ModCopy> enabled)
        {
            ModCopy best = null;
            if (enabled == null)
            {
                return null;
            }

            foreach (ModCopy c in enabled)
            {
                if (c != null && (best == null || Better(c, best)))
                {
                    best = c;
                }
            }

            return best;
        }

        private static bool Better(ModCopy a, ModCopy b)
        {
            int byVersion = (a.Version ?? new Version(0, 0)).CompareTo(
                b.Version ?? new Version(0, 0)
            );
            if (byVersion != 0)
            {
                return byVersion > 0;
            }

            if (a.Workshop != b.Workshop)
            {
                return !a.Workshop;
            }

            return string.CompareOrdinal(a.Path ?? "", b.Path ?? "") < 0;
        }

        private const string Product = "Finance Overhaul ";

        /// <summary>
        /// A copy by its version and where it is installed, e.g. "0.1.0 (Workshop)",
        /// "0.2.9816.30520 (local)": short, because the game's dialog has little room.
        /// </summary>
        public static string Describe(ModCopy copy)
        {
            string name = copy.Name ?? string.Empty;
            if (name.StartsWith(Product, StringComparison.Ordinal))
            {
                name = name.Substring(Product.Length);
            }

            name = name.Trim();
            return Strings.Format(
                copy.Workshop ? "duplicate.copy.workshop" : "duplicate.copy.local",
                name.Length == 0 || name == Product.Trim()
                    ? Strings.Get("duplicate.copy.unnamed")
                    : name
            );
        }

        public static string Title()
        {
            return Strings.Get("duplicate.title");
        }

        /// <summary>
        /// The question. With two copies, Yes keeps the suggested one and No keeps the other;
        /// with more, Yes keeps the suggested one and No changes nothing.
        /// </summary>
        public static string Message(ICollection<ModCopy> enabled)
        {
            ModCopy keep = Suggested(enabled);
            if (enabled.Count != 2)
            {
                return Strings.Format(
                    "duplicate.ask.many",
                    enabled.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Describe(keep)
                );
            }

            ModCopy other = null;
            foreach (ModCopy c in enabled)
            {
                if (c != keep)
                {
                    other = c;
                }
            }

            return Strings.Format("duplicate.ask.two", Describe(keep), Describe(other));
        }

        /// <summary>
        /// The copies to switch off for an answer. Yes: every copy but the suggested one.
        /// No with two copies: the suggested one (the player keeps the other). No with more
        /// than two: none.
        /// </summary>
        public static List<ModCopy> ToSwitchOff(ICollection<ModCopy> enabled, bool yes)
        {
            var off = new List<ModCopy>();
            if (!Conflict(enabled))
            {
                return off;
            }

            ModCopy keep = Suggested(enabled);
            foreach (ModCopy c in enabled)
            {
                if (yes ? c != keep : enabled.Count == 2 && c == keep)
                {
                    off.Add(c);
                }
            }

            return off;
        }
    }
}
