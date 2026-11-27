namespace FinanceOverhaul.Core
{
    /// <summary>Which side the game's Invest dialog opens on, or none.</summary>
    public enum DialogSide
    {
        None,
        Buy,
        Sell,
    }

    /// <summary>
    /// What a click on a stock's line opens (the Investments list, the overview's rows):
    /// Buy, or Sell when nothing is left to buy and the city holds some, else nothing (the
    /// game's dialog would open on an empty share range). Locked stocks and empty slots open
    /// nothing. Cities-free.
    /// </summary>
    public static class DialogSides
    {
        public static DialogSide ForLine(int slot, bool locked, uint available, uint owned)
        {
            if (slot <= 0 || locked)
            {
                return DialogSide.None;
            }

            if (available > 0)
            {
                return DialogSide.Buy;
            }

            return owned > 0 ? DialogSide.Sell : DialogSide.None;
        }
    }
}
