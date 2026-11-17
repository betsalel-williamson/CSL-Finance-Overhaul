using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class ShareAmountEditorTests
    {
        private static ShareAmountEditor Editing(uint original)
        {
            var e = new ShareAmountEditor();
            e.Begin(original);
            return e;
        }

        [Fact]
        public void Begin_RemembersOriginal()
        {
            var e = Editing(800u);
            Assert.True(e.IsEditing);
            Assert.Equal(800u, e.Original);
        }

        [Fact]
        public void TextChanged_ReturnsLiveClampedValue()
        {
            var e = Editing(800u);
            Assert.True(e.TryLiveValue("250", 0u, 1000u, out uint live));
            Assert.Equal(250u, live);
            Assert.True(e.TryLiveValue("5000", 0u, 1000u, out live));
            Assert.Equal(1000u, live);
        }

        [Fact]
        public void TextChanged_EmptyOrPartialDoesNotMoveSlider()
        {
            var e = Editing(800u);
            Assert.False(e.TryLiveValue("", 0u, 1000u, out _));
            Assert.False(e.TryLiveValue("12a", 0u, 1000u, out _));
        }

        [Fact]
        public void TextChanged_IgnoredWhenNotEditing()
        {
            var e = new ShareAmountEditor();
            Assert.False(e.TryLiveValue("250", 0u, 1000u, out _));
        }

        [Fact]
        public void Commit_AppliesParsedValueAndEndsEditing()
        {
            var e = Editing(800u);
            uint applied = e.Commit("1,200", 0u, 1000u);
            Assert.Equal(1000u, applied);
            Assert.False(e.IsEditing);
        }

        [Fact]
        public void Commit_InvalidTextKeepsOriginal()
        {
            var e = Editing(800u);
            Assert.Equal(800u, e.Commit("", 0u, 1000u));
            Assert.False(e.IsEditing);
        }

        [Fact]
        public void Cancel_RestoresOriginalAndEndsEditing()
        {
            var e = Editing(800u);
            e.TryLiveValue("250", 0u, 1000u, out _);
            Assert.Equal(800u, e.Cancel());
            Assert.False(e.IsEditing);
        }

        [Fact]
        public void CommitAfterCancel_IsNoOpReturningOriginal()
        {
            // Escape unfocuses the field, which also raises lost-focus → Commit.
            var e = Editing(800u);
            e.TryLiveValue("250", 0u, 1000u, out _);
            e.Cancel();
            Assert.Equal(800u, e.Commit("250", 0u, 1000u));
        }

        [Fact]
        public void SecondCommit_IsNoOp()
        {
            // Enter submits and then lost-focus fires a second commit.
            var e = Editing(800u);
            Assert.Equal(300u, e.Commit("300", 0u, 1000u));
            Assert.Equal(300u, e.Commit("999", 0u, 1000u));
        }
    }
}
