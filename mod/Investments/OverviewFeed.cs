using System.Collections.Generic; // List
using System.Text; // StringBuilder
using ColossalFramework.UI; // UIComponent, UILabel, UIPanel, UIScrollablePanel
// FinanceOverhaul.Core: FeedItem, FeedOverview, FeedTone, LocalCompany, MonitorQuote, Strings,
//   TradeOrder
using FinanceOverhaul.Core;
using UnityEngine; // Color32, Mathf, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// The Feed view: what is coming up (local companies about to trade, orders close to their
    /// trigger or about to end, stocks about to unlock) and what happened lately (big moves,
    /// 30-day highs and lows, new listings, this session's notices). The items scroll with the
    /// mouse wheel; a click on one about a stock opens its dialog. Items come from
    /// <see cref="FeedOverview"/>.
    /// </summary>
    internal sealed class OverviewFeed : IOverviewView
    {
        private const string Name = InvestmentsOverview.Name;
        private const float Pad = InvestmentsOverview.Pad;
        private const int ChartScale = InvestmentsOverview.ChartScale;

        private readonly InvestmentsOverview host;
        private List<MonitorQuote> quotes = new List<MonitorQuote>();

        public OverviewFeed(InvestmentsOverview host)
        {
            this.host = host;
            BuildFeed();
        }

        public UIPanel Panel
        {
            get { return feed; }
        }

        private const float FeedGap = 4f;
        private const float WhenWidth = 70f;

        private UIPanel feed;
        private UIScrollablePanel feedList;
        private UILabel upcomingHeading;
        private UILabel latelyHeading;
        private UILabel nothingLately;
        private readonly List<FeedRowView> feedRows = new List<FeedRowView>();

        /// <summary>The companies the Feed reads (none while Local IPOs are off).</summary>
        private static List<LocalCompany> FeedCompanies()
        {
            return CompanyDesk.Enabled ? CompanyDesk.Companies() : new List<LocalCompany>();
        }

        private void BuildFeed()
        {
            feed = host.NewView("Feed");
            feedList = InvestmentsOverview.Scroller(feed, Name + "FeedItems", Pad);
            upcomingHeading = InvestmentsOverview.Heading(
                feedList,
                Strings.Get("overview.feed.upcoming"),
                0f,
                0f
            );
            upcomingHeading.name = Name + "FeedUpcoming";
            latelyHeading = InvestmentsOverview.Heading(
                feedList,
                Strings.Get("overview.feed.lately"),
                0f,
                0f
            );
            latelyHeading.name = Name + "FeedLately";
            nothingLately = FoText.Label(
                feedList,
                Name + "FeedNothingLately",
                "",
                0.75f,
                FoTheme.TextMuted
            );
        }

        /// <summary>What else, besides prices and orders, makes this view draw again.</summary>
        public void Sign(StringBuilder sig)
        {
            sig.Append("|n").Append(ChirpNotices.Log.Snapshot().Count);
            foreach (LocalCompany c in FeedCompanies())
            {
                sig.Append("|c").Append(c.Symbol).Append(':').Append(c.Status);
            }
        }

        public void Draw(OverviewFrame frame)
        {
            quotes = frame.Quotes;
            int today = OrderDesk.Today;
            List<LocalCompany> companies = FeedCompanies();
            List<TradeOrder> orders =
                InvestmentsOverview.TestOrders != null
                    ? new List<TradeOrder>(InvestmentsOverview.TestOrders)
                    : new List<TradeOrder>(OrderDesk.Snapshot());
            List<FeedItem> upcoming = FeedOverview.Upcoming(quotes, orders, companies, today);
            List<FeedItem> lately = FeedOverview.Lately(
                quotes,
                ChirpNotices.Log.Snapshot(),
                companies,
                today
            );

            float width = feedList.width;
            int used = 0;
            float y = 0f;
            // "Coming up" only when something is: an offering, an order near its trigger or
            // its end, a stock about to unlock. Otherwise the feed starts at "Lately".
            bool anyUpcoming = upcoming.Count > 0;
            upcomingHeading.isVisible = anyUpcoming;
            if (anyUpcoming)
            {
                upcomingHeading.relativePosition = new Vector3(0f, y);
                y += 20f;
                y = Items(upcoming, y, width, ref used);
                y += 8f;
            }

            latelyHeading.relativePosition = new Vector3(0f, y);
            y += 20f;
            nothingLately.isVisible = lately.Count == 0;
            if (lately.Count == 0)
            {
                FoText.Fit(
                    nothingLately,
                    Strings.Get("overview.feed.nothingLately"),
                    0.75f,
                    0.65f,
                    width,
                    14f
                );
                nothingLately.relativePosition = new Vector3(0f, y);
            }

            Items(lately, y, width, ref used);
            for (int i = used; i < feedRows.Count; i++)
            {
                feedRows[i].Hide();
            }
        }

        private float Items(List<FeedItem> items, float y, float width, ref int used)
        {
            foreach (FeedItem item in items)
            {
                if (used == feedRows.Count)
                {
                    feedRows.Add(new FeedRowView(feedList, used, width));
                }

                y += feedRows[used].Show(item, QuoteOf(item.Symbol), y) + FeedGap;
                used++;
            }

            return y;
        }

        private MonitorQuote QuoteOf(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return null;
            }

            foreach (MonitorQuote q in quotes)
            {
                if (q.Symbol == symbol)
                {
                    return q;
                }
            }

            return null;
        }

        private static Color32 ToneColour(FeedTone tone)
        {
            switch (tone)
            {
                case FeedTone.Gain:
                    return FoTheme.GamePositive;
                case FeedTone.Loss:
                    return FoTheme.GameNegative;
                case FeedTone.Accent:
                    return FoTheme.Accent;
                default:
                    return FoTheme.TickerFlat;
            }
        }

        /// <summary>One item: a bar in its tone, the text wrapped, when it happens on the right.</summary>
        private sealed class FeedRowView
        {
            private readonly UIPanel back;
            private readonly UIPanel bar;
            private readonly UILabel text;
            private readonly UILabel when;
            private readonly float width;
            private MonitorQuote quote;

            private string chirpSender;
            private string chirpText;

            private void Open()
            {
                if (chirpSender != null)
                {
                    ChirpNotices.Show(chirpSender, chirpText);
                }
                else
                {
                    InvestmentsOverview.OpenDialog(quote);
                }
            }

            public FeedRowView(UIComponent parent, int index, float width)
            {
                this.width = width;
                string name = Name + "Feed-" + index;
                // A chirp opens in the Chirper; an item the feed worked out about a stock opens
                // the stock. It lights up when a click does either.
                back = FoRow.Add(
                    parent,
                    name,
                    new Vector2(width, 26f),
                    Vector3.zero,
                    p => Open(),
                    () => chirpSender != null || quote != null
                );
                bar = back.AddUIComponent<UIPanel>();
                bar.name = name + "Tone";
                bar.backgroundSprite = "EmptySprite"; // not shown: a game sprite name
                bar.size = new Vector2(3f, 26f);
                bar.relativePosition = Vector3.zero;
                bar.isInteractive = false;
                text = FoText.Label(back, name + "Text", "", 0.75f, FoTheme.GameText);
                text.relativePosition = new Vector3(10f, 5f);
                text.isInteractive = false;
                when = FoText.Label(back, name + "When", "", 0.65f, FoTheme.TextMuted);
                when.isInteractive = false;
            }

            /// <summary>Fills the item at <paramref name="y"/>; returns its height.</summary>
            public float Show(FeedItem item, MonitorQuote q, float y)
            {
                quote = q;
                chirpSender = item.ChirpSender;
                chirpText = item.Text;
                back.isVisible = true;
                back.relativePosition = new Vector3(0f, y);
                bar.color = ToneColour(item.Tone);
                when.text = item.When ?? string.Empty;
                when.relativePosition = new Vector3(width - 8f - when.width, 6f);
                float h = FoText.Fit(text, item.Text, 0.75f, 0.65f, width - 18f - WhenWidth, 14f);
                float height = Mathf.Max(26f, h + 10f);
                back.height = height;
                bar.height = height;
                back.tooltip =
                    chirpSender != null ? Strings.Get("overview.feed.openChirp")
                    : q == null ? null
                    : q.Name;
                return height;
            }

            public void Hide()
            {
                back.isVisible = false;
                quote = null;
            }
        }
    }
}
