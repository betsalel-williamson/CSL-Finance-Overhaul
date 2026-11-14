using System;
using System.Globalization;
using System.Text;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Plain-text build identity for the Options "Copy build info" button: one fact per
    /// line so it pastes cleanly into a bug report or chat. Unknown parts show "?".
    /// </summary>
    public static class BuildInfoText
    {
        public static string Format(
            string productVersion,
            string assemblyVersion,
            string builtUtc,
            bool devBuild
        )
        {
            var sb = new StringBuilder();
            sb.Append("Finance Overhaul ").Append(OrUnknown(productVersion)).Append('\n');
            sb.Append("Assembly: ").Append(OrUnknown(assemblyVersion)).Append('\n');
            string local = LocalTime(builtUtc);
            sb.Append("Built (UTC): ").Append(local == null ? "?" : builtUtc).Append('\n');
            if (local != null)
            {
                sb.Append("Built (local): ").Append(local).Append('\n');
            }

            sb.Append("Build: ").Append(devBuild ? "dev" : "release");
            return sb.ToString();
        }

        /// <summary>UTC ISO timestamp → this machine's local time with offset, or null.</summary>
        public static string LocalTime(string builtUtc)
        {
            DateTime utc;
            if (
                string.IsNullOrEmpty(builtUtc)
                || !DateTime.TryParse(
                    builtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out utc
                )
            )
            {
                return null;
            }

            return DateTime
                .SpecifyKind(utc, DateTimeKind.Utc)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        }

        private static string OrUnknown(string value)
        {
            return string.IsNullOrEmpty(value) ? "?" : value;
        }
    }
}
