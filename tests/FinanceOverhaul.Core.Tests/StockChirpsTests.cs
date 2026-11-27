using System.Collections.Generic; // KeyValuePair, List
using FinanceOverhaul.Core; // FeedLog, Notice, NoticeKind, StockChirp, StockChirps
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class StockChirpsTests
    {
        [Fact]
        public void TheStocksNoticesNewestFirstThenTheGamesOwn()
        {
            var log = new FeedLog();
            log.Add(
                10,
                new Notice
                {
                    Kind = NoticeKind.Filled,
                    Symbol = "OIL",
                    Sender = "City Brokerage",
                    Text = "Bought 10 OIL",
                }
            );
            log.Add(
                11,
                new Notice
                {
                    Kind = NoticeKind.Filled,
                    Symbol = "ORE",
                    Sender = "City Brokerage",
                    Text = "Bought 5 ORE",
                }
            );
            log.Add(
                12,
                new Notice
                {
                    Kind = NoticeKind.Expired,
                    Symbol = "OIL",
                    Sender = "City Brokerage",
                    Text = "OIL order ended",
                }
            );
            var game = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Ella", "Oil is soaring!"),
                new KeyValuePair<string, string>("Bob", ""),
            };

            List<StockChirp> chirps = StockChirps.For("OIL", log.Snapshot(), game, 12);
            Assert.Equal(3, chirps.Count);
            Assert.Equal("OIL order ended", chirps[0].Text);
            Assert.Equal("Today", chirps[0].When);
            Assert.Equal("Bought 10 OIL", chirps[1].Text);
            Assert.Equal("2 days ago", chirps[1].When);
            Assert.Equal("City Brokerage", chirps[1].Sender);
            Assert.Equal("Ella", chirps[2].Sender);
            Assert.Equal("Recently", chirps[2].When);
            Assert.Empty(StockChirps.For("XYZ", log.Snapshot(), null, 12));
        }
    }
}
