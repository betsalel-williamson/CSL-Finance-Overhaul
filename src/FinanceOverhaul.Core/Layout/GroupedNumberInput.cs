using System.Text;

namespace FinanceOverhaul.Core
{
    /// <summary>A typed whole number after grouping: its text and where the caret belongs.</summary>
    public struct GroupedEdit
    {
        public string Text;
        public int Caret;
    }

    /// <summary>
    /// Keeps a typed whole number grouped in thousands while the player edits it ("1500000"
    /// reads "1,500,000"), with the caret staying beside the digit it was beside. The
    /// separators are the field's, not the player's: Backspace or Delete on one removes the
    /// digit next to it instead, so nobody has to delete commas by hand.
    /// </summary>
    public static class GroupedNumberInput
    {
        /// <summary>
        /// The field's text and caret after an edit, regrouped.
        /// </summary>
        /// <param name="before">The text before the edit (null when unknown).</param>
        /// <param name="caretBefore">The caret before the edit.</param>
        /// <param name="after">The text as the field left it.</param>
        /// <param name="caretAfter">The caret as the field left it.</param>
        /// <param name="separator">The thousands separator, from the string table.</param>
        /// <param name="maxDigits">Digits beyond this many are dropped.</param>
        public static GroupedEdit Apply(
            string before,
            int caretBefore,
            string after,
            int caretAfter,
            string separator,
            int maxDigits
        )
        {
            after = after ?? string.Empty;
            caretAfter = Clamp(caretAfter, 0, after.Length);
            RemoveDigitBesideDeletedSeparator(before, caretBefore, ref after, ref caretAfter);

            int digitsLeft = 0;
            var digits = new StringBuilder();
            for (int i = 0; i < after.Length; i++)
            {
                if (IsDigit(after[i]))
                {
                    digits.Append(after[i]);
                    if (i < caretAfter)
                    {
                        digitsLeft++;
                    }
                }
            }

            // "0500" is 500; a lone 0 stays so the field does not empty under the player.
            int zeros = 0;
            while (zeros < digits.Length - 1 && digits[zeros] == '0')
            {
                zeros++;
            }

            digits.Remove(0, zeros);
            digitsLeft = System.Math.Max(0, digitsLeft - zeros);
            if (maxDigits > 0 && digits.Length > maxDigits)
            {
                digits.Length = maxDigits;
            }

            digitsLeft = System.Math.Min(digitsLeft, digits.Length);
            string text = Group(digits.ToString(), separator);
            int caret = 0;
            for (int seen = 0; seen < digitsLeft && caret < text.Length; caret++)
            {
                if (IsDigit(text[caret]))
                {
                    seen++;
                }
            }

            return new GroupedEdit { Text = text, Caret = caret };
        }

        /// <summary>Digits in groups of three from the right: "1500000" is "1,500,000".</summary>
        public static string Group(string digits, string separator)
        {
            if (string.IsNullOrEmpty(digits) || string.IsNullOrEmpty(separator))
            {
                return digits ?? string.Empty;
            }

            var grouped = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                {
                    grouped.Append(separator);
                }

                grouped.Append(digits[i]);
            }

            return grouped.ToString();
        }

        /// <summary>
        /// When the edit removed exactly one separator, the player pressed Backspace just
        /// after it or Delete just before it: take the digit on that side instead.
        /// </summary>
        private static void RemoveDigitBesideDeletedSeparator(
            string before,
            int caretBefore,
            ref string after,
            ref int caretAfter
        )
        {
            if (before == null || after.Length != before.Length - 1)
            {
                return;
            }

            int at = 0;
            while (at < after.Length && before[at] == after[at])
            {
                at++;
            }

            if (IsDigit(before[at]) || before.Remove(at, 1) != after)
            {
                return;
            }

            if (caretBefore == at + 1)
            {
                // Backspace: the digit left of the separator.
                for (int i = at - 1; i >= 0; i--)
                {
                    if (IsDigit(after[i]))
                    {
                        after = after.Remove(i, 1);
                        caretAfter = i;
                        return;
                    }
                }
            }
            else
            {
                // Delete: the digit right of the separator.
                for (int i = at; i < after.Length; i++)
                {
                    if (IsDigit(after[i]))
                    {
                        after = after.Remove(i, 1);
                        caretAfter = at;
                        return;
                    }
                }
            }
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min
                : value > max ? max
                : value;
        }
    }
}
