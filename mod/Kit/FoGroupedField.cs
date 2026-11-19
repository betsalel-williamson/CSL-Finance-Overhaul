using System.Reflection; // FieldInfo
using ColossalFramework.UI; // UITextField
using FinanceOverhaul.Core; // GroupedEdit, GroupedNumberInput, Strings

namespace FinanceOverhaul
{
    /// <summary>
    /// Keeps a typed whole number grouped in thousands ("1,500,000") with the caret beside
    /// the digit it was beside; the rule is the library's <see cref="GroupedNumberInput"/>.
    /// The owner calls <see cref="Tick"/> once a frame: by then the field has finished its
    /// own handling of the key, so the text and caret read here are settled.
    /// </summary>
    internal sealed class FoGroupedField
    {
        private static readonly FieldInfo CursorIndex = GameInternals.Ui.TextCursorIndex;
        private static readonly FieldInfo SelectionStart = GameInternals.Ui.TextSelectionStart;
        private static readonly FieldInfo SelectionEnd = GameInternals.Ui.TextSelectionEnd;

        private readonly UITextField field;
        private readonly int maxDigits;
        private string seenText;
        private int seenCaret;

        /// <param name="maxDigits">Digits typed beyond this many are dropped.</param>
        public FoGroupedField(UITextField field, int maxDigits)
        {
            this.field = field;
            this.maxDigits = maxDigits;
        }

        public void Tick()
        {
            if (field == null || CursorIndex == null)
            {
                return;
            }

            string text = field.text ?? string.Empty;
            int caret = (int)CursorIndex.GetValue(field);
            if (text != seenText)
            {
                GroupedEdit edit = GroupedNumberInput.Apply(
                    seenText,
                    seenCaret,
                    text,
                    caret,
                    Strings.Get("number.group"),
                    maxDigits
                );
                if (edit.Text != text)
                {
                    field.text = edit.Text; // raises the field's own change event
                }

                if (field.hasFocus)
                {
                    CursorIndex.SetValue(field, edit.Caret);
                    if (SelectionStart != null && SelectionEnd != null)
                    {
                        SelectionStart.SetValue(field, edit.Caret);
                        SelectionEnd.SetValue(field, edit.Caret);
                    }

                    field.Invalidate();
                }

                text = edit.Text;
                caret = edit.Caret;
            }

            seenText = text;
            seenCaret = caret;
        }
    }
}
