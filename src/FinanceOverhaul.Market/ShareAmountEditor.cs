namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Edit session for the typed share amount field. The Host forwards focus,
    /// text, and key events; this type decides what the slider should show.
    /// <list type="bullet">
    /// <item>Begin (focus): remember the original count.</item>
    /// <item>Typing: a valid number moves the slider live (clamped).</item>
    /// <item>Enter / Tab / click outside: Commit applies the typed value.</item>
    /// <item>Escape: Cancel restores the original count.</item>
    /// </list>
    /// Commit after Cancel or a second Commit is a no-op, because unfocusing the
    /// field raises lost-focus after the key that ended the edit.
    /// </summary>
    public sealed class ShareAmountEditor
    {
        private uint lastResult;

        public bool IsEditing { get; private set; }

        public uint Original { get; private set; }

        public void Begin(uint current)
        {
            Original = current;
            lastResult = current;
            IsEditing = true;
        }

        public bool TryLiveValue(string text, uint min, uint max, out uint value)
        {
            if (!IsEditing)
            {
                value = 0u;
                return false;
            }

            return ShareAmountInput.TryParse(text, min, max, out value);
        }

        public uint Commit(string text, uint min, uint max)
        {
            if (!IsEditing)
            {
                return lastResult;
            }

            IsEditing = false;
            lastResult = ShareAmountInput.TryParse(text, min, max, out uint value)
                ? value
                : Original;
            return lastResult;
        }

        public uint Cancel()
        {
            IsEditing = false;
            lastResult = Original;
            return Original;
        }
    }
}
