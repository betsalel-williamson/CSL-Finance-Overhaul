using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class GroupedNumberInputTests
    {
        private static GroupedEdit Edit(
            string before,
            int caretBefore,
            string after,
            int caretAfter
        )
        {
            return GroupedNumberInput.Apply(before, caretBefore, after, caretAfter, ",", 10);
        }

        [Fact]
        public void TypingADigitInFrontRegroups()
        {
            // "500,000", caret at the start, type 1.
            GroupedEdit e = Edit("500,000", 0, "1500,000", 1);
            Assert.Equal("1,500,000", e.Text);
            Assert.Equal(1, e.Caret);
        }

        [Theory]
        [InlineData("", 0, "5", 1, "5", 1)]
        [InlineData("999", 3, "9990", 4, "9,990", 5)]
        [InlineData("1,000", 5, "1,0005", 6, "10,005", 6)]
        [InlineData("12,345", 2, "127,345", 3, "127,345", 3)]
        [InlineData("1,500,000", 9, "1,500,00", 8, "150,000", 7)]
        public void TheCaretStaysBesideTheSameDigit(
            string before,
            int caretBefore,
            string after,
            int caretAfter,
            string text,
            int caret
        )
        {
            GroupedEdit e = Edit(before, caretBefore, after, caretAfter);
            Assert.Equal(text, e.Text);
            Assert.Equal(caret, e.Caret);
        }

        [Fact]
        public void BackspaceOnASeparatorDeletesTheDigitBeforeIt()
        {
            // "1,|500,000": the field removed the comma; the 1 goes instead.
            GroupedEdit e = Edit("1,500,000", 2, "1500,000", 1);
            Assert.Equal("500,000", e.Text);
            Assert.Equal(0, e.Caret);
        }

        [Fact]
        public void DeleteOnASeparatorDeletesTheDigitAfterIt()
        {
            // "1|,500,000": the field removed the comma; the 5 goes instead.
            GroupedEdit e = Edit("1,500,000", 1, "1500,000", 1);
            Assert.Equal("100,000", e.Text);
            Assert.Equal(1, e.Caret);
        }

        [Fact]
        public void BackspacingFromTheEndNeverStopsAtASeparator()
        {
            string text = "500,000";
            int caret = text.Length;
            for (int presses = 0; presses < 6; presses++)
            {
                // What the field does on Backspace: remove the character before the caret.
                string after = text.Remove(caret - 1, 1);
                GroupedEdit e = Edit(text, caret, after, caret - 1);
                text = e.Text;
                caret = e.Caret;
            }

            Assert.Equal(string.Empty, text);
        }

        [Theory]
        [InlineData("1500000", "1,500,000")]
        [InlineData("1 500 000", "1,500,000")]
        [InlineData("1.500.000", "1,500,000")]
        [InlineData("₡250,000", "250,000")]
        [InlineData("000", "0")]
        [InlineData("0500", "500")]
        [InlineData("abc", "")]
        [InlineData("123456789012345", "1,234,567,890")]
        public void AnythingPastedOrSetReadsAsOneGroupedNumber(string typed, string text)
        {
            Assert.Equal(text, Edit(null, 0, typed, typed.Length).Text);
        }

        [Fact]
        public void AnotherLanguagesSeparatorIsUsedAsGiven()
        {
            GroupedEdit e = GroupedNumberInput.Apply("500.000", 0, "1500.000", 1, ".", 10);
            Assert.Equal("1.500.000", e.Text);
            Assert.Equal(1, e.Caret);
        }
    }
}
