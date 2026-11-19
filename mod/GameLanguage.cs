using System; // Exception
using ColossalFramework.Globalization; // LocaleManager
using FinanceOverhaul.Core; // Strings

namespace FinanceOverhaul
{
    /// <summary>
    /// Keeps the mod's wording (<see cref="Strings"/>) in the language the game is set to,
    /// including when the player changes it in Options. A language the mod has no table for
    /// reads as English.
    /// </summary>
    internal static class GameLanguage
    {
        private static bool following;

        public static void Follow()
        {
            Apply();
            if (following)
            {
                return;
            }

            try
            {
                LocaleManager.eventLocaleChanged += Apply;
                following = true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("language: cannot follow the game's language: " + ex.Message);
            }
        }

        public static void Unfollow()
        {
            if (!following)
            {
                return;
            }

            try
            {
                LocaleManager.eventLocaleChanged -= Apply;
            }
            catch
            {
                // fail soft
            }

            following = false;
        }

        private static void Apply()
        {
            try
            {
                string code = LocaleManager.exists ? LocaleManager.instance.language : null;
                Strings.Language = code;
            }
            catch
            {
                Strings.Language = Strings.DefaultLanguage;
            }
        }
    }
}
