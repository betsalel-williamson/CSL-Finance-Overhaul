using System.Collections.Generic; // List
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
    internal sealed partial class InvestmentsOverview
    {
        private const float FeedGap = 4f;
        private const float WhenWidth = 70f;

        private UIPanel feed;
        private UIScrollablePanel feedList;
        private UILabel upcomingHeading;
        private UILabel latelyHeading;
        private UILabel nothingUpcoming;
        private UILabel nothingLately;
        private readonly List<FeedRowView> feedRows = new List<FeedRowView>();

        /// <summary>The companies the Feed reads (none while Local IPOs are off).</summary>
        private static List<LocalCompany> FeedCompanies()
        {
            return CompanyDesk.Enabled ? CompanyDesk.Companies() : new List<LocalCompany>();
        }

        private void BuildFeed()
        {
            feed = View("Feed");
            feedList = Scroller(feed, Name + "FeedItems", Pad);
            upcomingHeading = Heading(feedList, Strings.Get("overview.feed.upcoming"), 0f, 0f);
            upcomingHeading.name = Name + "FeedUpcoming";
            latelyHeading = Heading(feedList, Strings.Get("overview.feed.lately"), 0f, 0f);
            latelyHeading.name = Name + "FeedLately";
            nothingUpcoming = FoText.Label(
                feedList,
                Name + "FeedNothingUpcoming",
                Strings.Get("overview.feed.nothingUpcoming"),
                0.75f,
                FoTheme.TextMuted
            );
            nothingLately = FoText.Label(
                feedList,
                Name + "FeedNothingLately",
                "",
                0.75f,
                FoTheme.TextMuted
            );
        }

        private void DrawFeed()
        {
            int today = OrderDesk.Today;
            List<LocalCompany> companies = FeedCompanies();
            List<TradeOrder> orders =
                TestOrders != null
                    ? new List<TradeOrder>(TestOrders)
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
            upcomingHeading.relativePosition = new Vector3(0f, y);
            y += 20f;
            nothingUpcoming.isVisible = upcoming.Count == 0;
            if (upcoming.Count == 0)
            {
                nothingUpcoming.relativePosition = new Vector3(0f, y);
                y += 22f;
            }

            y = Items(upcoming, y, width, ref used);
            y += 8f;
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

            public FeedRowView(UIComponent parent, int index, float width)
            {
                this.width = width;
                string name = Name + "Feed-" + index;
                back = parent.AddUIComponent<UIPanel>();
                back.name = name;
                back.backgroundSprite = "GenericPanelWhite";
                back.color = FoTheme.SectionFill;
                back.size = new Vector2(width, 26f);
                back.eventMouseEnter += (c, p) =>
                {
                    if (quote != null)
                    {
                        back.color = FoTheme.SectionFillHover;
                    }
                };
                back.eventMouseLeave += (c, p) => back.color = FoTheme.SectionFill;
                back.eventClick += (c, p) => OpenDialog(quote);
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
                back.isVisible = true;
                back.relativePosition = new Vector3(0f, y);
                bar.color = ToneColour(item.Tone);
                when.text = item.When ?? string.Empty;
                when.relativePosition = new Vector3(width - 8f - when.width, 6f);
                float h = FoText.Fit(text, item.Text, 0.75f, 0.65f, width - 18f - WhenWidth, 14f);
                float height = Mathf.Max(26f, h + 10f);
                back.height = height;
                bar.height = height;
                back.tooltip = q == null ? null : q.Name;
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
