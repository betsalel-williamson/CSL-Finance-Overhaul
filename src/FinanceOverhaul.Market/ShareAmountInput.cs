using System.Globalization;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Parses a typed share count from the Invest dialog field. Whole numbers only
    /// (thousands separators allowed); out-of-range values clamp to
    /// [<paramref name="min"/>, <paramref name="max"/>] so the Host can feed the
    /// result straight into the vanilla slider.
    /// </summary>
    public static class ShareAmountInput
    {
        public static bool TryParse(string text, uint min, uint max, out uint value)
        {
            value = 0u;
            if (text == null)
            {
                return false;
            }

            string trimmed = text.Trim().Replace(",", string.Empty);
            if (trimmed.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            if (max < min)
            {
                max = min;
            }

            ulong parsed;
            if (
                trimmed.Length > 19
                || !ulong.TryParse(
                    trimmed,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parsed
                )
            )
            {
                // All digits but too long for ulong — treat as "very large".
                value = max;
                return true;
            }

            if (parsed < min)
            {
                value = min;
            }
            else if (parsed > max)
            {
                value = max;
            }
            else
            {
                value = (uint)parsed;
            }

            return true;
        }
    }
}
