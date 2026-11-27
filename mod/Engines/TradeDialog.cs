using FinanceOverhaul.Core; // DialogSide, DialogSides

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel

namespace FinanceOverhaul
{
    /// <summary>
    /// Opens the game's Invest dialog on a stock: the one place the mod's surfaces (the
    /// Investments list and overview, the Stock Monitor, the company panels and the IPO
    /// dialog) ask for it. Which side a line opens is the library's rule
    /// (<see cref="DialogSides"/>).
    /// </summary>
    internal static class TradeDialog
    {
        /// <summary>Opens the dialog on <paramref name="slot"/> at <paramref name="side"/>; nothing for None.</summary>
        public static void Show(int slot, DialogSide side)
        {
            if (side == DialogSide.None)
            {
                return;
            }

            BuySellInvestmentPanel.ShowPanel(
                (byte)slot,
                side == DialogSide.Sell
                    ? BuySellInvestmentPanel.Mode.Sell
                    : BuySellInvestmentPanel.Mode.Buy
            );
        }

        /// <summary>What a click on a stock's line does (<see cref="DialogSides.ForLine"/>).</summary>
        public static void ShowForLine(int slot, bool locked, uint available, uint owned)
        {
            Show(slot, DialogSides.ForLine(slot, locked, available, owned));
        }
    }
}
