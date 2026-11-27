using System.Collections.Generic; // IEnumerable, KeyValuePair, List

namespace FinanceOverhaul.Core
{
    /// <summary>One chirp in a stock's Chirps tab.</summary>
    public struct StockChirp
    {
        public string Sender;
        public string Text;

        /// <summary>"Today", "3 days ago"; "Recently" for the game's own (it keeps no day).</summary>
        public string When;
    }

    /// <summary>
    /// A stock's chirps for the Invest dialog's Chirps tab: the mod's notices about it this
    /// session (fills, expiries, company news; newest first) and the game's own chirps about
    /// it still in its recent messages (rising, falling). The game keeps only its last 16
    /// messages and the mod's notices are not saved, so this is what is recent. Cities-free.
    /// </summary>
    public static class StockChirps
    {
        /// <param name="symbol">The stock's symbol, as the notices carry it.</param>
        /// <param name="log">The session's notices (<see cref="FeedLog.Snapshot"/>).</param>
        /// <param name="gameChirps">The game's chirps about the stock: sender and text, newest first.</param>
        public static List<StockChirp> For(
            string symbol,
            IEnumerable<FeedEntry> log,
            IEnumerable<KeyValuePair<string, string>> gameChirps,
            int today
        )
        {
            var chirps = new List<StockChirp>();
            var mine = new List<FeedEntry>();
            foreach (FeedEntry e in log ?? new List<FeedEntry>())
            {
                if (!string.IsNullOrEmpty(symbol) && e.Symbol == symbol)
                {
                    mine.Add(e);
                }
            }

            // Newest first: the log runs oldest first.
            for (int i = mine.Count - 1; i >= 0; i--)
            {
                chirps.Add(
                    new StockChirp
                    {
                        Sender = mine[i].Sender ?? string.Empty,
                        Text = mine[i].Text,
                        When = FeedOverview.Ago(today - mine[i].Day),
                    }
                );
            }

            foreach (
                KeyValuePair<string, string> g in gameChirps
                    ?? new List<KeyValuePair<string, string>>()
            )
            {
                if (!string.IsNullOrEmpty(g.Value))
                {
                    chirps.Add(
                        new StockChirp
                        {
                            Sender = g.Key ?? string.Empty,
                            Text = g.Value,
                            When = Strings.Get("chirps.recently"),
                        }
                    );
                }
            }

            return chirps;
        }
    }
}
