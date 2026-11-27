using System; // Math, StringComparison
using System.Collections.Generic; // Dictionary, IEnumerable, KeyValuePair, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>One notice the mod posted, kept for the Feed with the day it was posted.</summary>
    public struct FeedEntry
    {
        public int Day;
        public NoticeKind Kind;
        public string Symbol;
        public string Text;
    }

    /// <summary>
    /// The notices of this session (fills, expiries, bonds, companies), newest last, at most
    /// <see cref="Capacity"/>. Not saved: the Feed's "Lately" starts again after a load, as
    /// the Chirper does. Thread-safe (the simulation thread adds, the UI reads).
    /// </summary>
    public sealed class FeedLog
    {
        public const int Capacity = 40;

        private readonly object sync = new object();
        private readonly List<FeedEntry> entries = new List<FeedEntry>();

        public void Add(int day, Notice notice)
        {
            if (notice == null || string.IsNullOrEmpty(notice.Text))
            {
                return;
            }

            lock (sync)
            {
                entries.Add(
                    new FeedEntry
                    {
                        Day = day,
                        Kind = notice.Kind,
                        Symbol = notice.Symbol,
                        Text = notice.Text,
                    }
                );
                if (entries.Count > Capacity)
                {
                    entries.RemoveRange(0, entries.Count - Capacity);
                }
            }
        }

        public List<FeedEntry> Snapshot()
        {
            lock (sync)
            {
                return new List<FeedEntry>(entries);
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                entries.Clear();
            }
        }
    }

    /// <summary>How a Feed item is marked: its colour bar.</summary>
    public enum FeedTone
    {
        Neutral,
        Gain,
        Loss,
        Accent,
    }

    /// <summary>One line of the Feed.</summary>
    public struct FeedItem
    {
        public string Text;

        /// <summary>"Today", "3 days ago", "in 5 days"; empty when it has no day.</summary>
        public string When;

        /// <summary>The stock it is about (a click opens it), or null.</summary>
        public string Symbol;

        public FeedTone Tone;

        /// <summary>Sort key: days from today (upcoming), or days ago (lately).</summary>
        public int Days;

        /// <summary>On one day: big moves first, then highs and lows, listings, notices.</summary>
        public int Rank;
    }

    /// <summary>
    /// The Feed view of the overview (Figma band 20, "Two columns"): what is coming up (local
    /// companies about to trade, orders close to their trigger or about to end, stocks about
    /// to unlock) and what happened lately (big moves, 30-day highs and lows, new listings,
    /// and this session's notices). All read from what the game and the mod already keep.
    /// Cities-free.
    /// </summary>
    public static class FeedOverview
    {
        /// <summary>A change rate at least this big (in percent, either way) is a big move.</summary>
        public const float BigMovePercent = 10f;

        /// <summary>An order whose stock is within this much (in percent) of its trigger is close.</summary>
        public const float NearTriggerPercent = 5f;

        /// <summary>An order ending within this many days is listed.</summary>
        public const int EndingDays = 7;

        /// <summary>A locked stock this far along its milestone unlocks soon.</summary>
        public const float UnlockSoon = 0.75f;

        /// <summary>"Lately" reaches back this many days.</summary>
        public const int LatelyDays = 30;

        /// <summary>A 30-day high or low needs at least this many days of prices.</summary>
        public const int HighLowMinDays = 10;

        /// <summary>
        /// What is coming up, soonest first: local companies in their offering window, orders
        /// within <see cref="NearTriggerPercent"/> of their trigger, orders ending within
        /// <see cref="EndingDays"/>, and stocks at least <see cref="UnlockSoon"/> unlocked.
        /// </summary>
        public static List<FeedItem> Upcoming(
            IEnumerable<MonitorQuote> quotes,
            IEnumerable<TradeOrder> orders,
            IEnumerable<LocalCompany> companies,
            int today
        )
        {
            var items = new List<FeedItem>();
            var byKey = new Dictionary<string, MonitorQuote>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q != null && q.Prefab != null && !byKey.ContainsKey(q.Prefab))
                {
                    byKey[q.Prefab] = q;
                }
            }

            foreach (LocalCompany c in companies ?? new List<LocalCompany>())
            {
                if (c == null || c.Status != CompanyStatus.Offering)
                {
                    continue;
                }

                int days = Math.Max(0, c.FirstTradingDay - today);
                items.Add(
                    new FeedItem
                    {
                        Text = Strings.Format("overview.feed.ipoSoon", c.Symbol, c.Name),
                        When = InDays(days),
                        Symbol = c.Symbol,
                        Tone = FeedTone.Accent,
                        Days = days,
                    }
                );
            }

            foreach (TradeOrder o in orders ?? new List<TradeOrder>())
            {
                if (o == null)
                {
                    continue;
                }

                MonitorQuote q;
                byKey.TryGetValue(o.InvestmentKey ?? string.Empty, out q);
                string symbol = q != null ? q.Symbol : o.InvestmentKey;
                string kind = Strings.Get("overview.feed.order." + OrderText.Key(o.Kind));
                float gap = q == null ? float.NaN : TriggerGap(o, q.Price);
                if (!float.IsNaN(gap) && gap <= NearTriggerPercent)
                {
                    items.Add(
                        new FeedItem
                        {
                            Text = Strings.Format(
                                "overview.feed.nearTrigger",
                                symbol,
                                gap.ToString("0.0", CultureInfo.InvariantCulture),
                                kind,
                                StockMonitor.PriceText(o.TriggerPrice)
                            ),
                            When = Strings.Get("overview.feed.when.now"),
                            Symbol = symbol,
                            Tone = o.IsSell ? FeedTone.Loss : FeedTone.Gain,
                            Days = 0,
                        }
                    );
                }

                int left = o.ExpiresDay - today;
                if (left >= 0 && left <= EndingDays)
                {
                    items.Add(
                        new FeedItem
                        {
                            Text = Strings.Format("overview.feed.ending", symbol, kind),
                            When = InDays(left),
                            Symbol = symbol,
                            Tone = FeedTone.Neutral,
                            Days = left,
                        }
                    );
                }
            }

            foreach (MonitorQuote q in byKey.Values)
            {
                if (q.Locked && q.UnlockProgress >= UnlockSoon)
                {
                    items.Add(
                        new FeedItem
                        {
                            Text = string.IsNullOrEmpty(q.UnlockText)
                                ? Strings.Format("overview.feed.unlockSoon", q.Symbol)
                                : Strings.Format(
                                    "overview.feed.unlockSoonWhy",
                                    q.Symbol,
                                    q.UnlockText
                                ),
                            When = StockMonitor.PercentText(q.UnlockProgress),
                            Symbol = q.Symbol,
                            Tone = FeedTone.Accent,
                            Days = EndingDays + 1,
                        }
                    );
                }
            }

            items.Sort(Soonest);
            return items;
        }

        /// <summary>
        /// How far the stock is from setting the order off, in percent of its price: 0 when it
        /// already would. NaN without a price.
        /// </summary>
        public static float TriggerGap(TradeOrder order, float price)
        {
            if (order == null || float.IsNaN(price) || price <= 0f)
            {
                return float.NaN;
            }

            float trigger = order.TriggerPrice;
            // A take-profit fires at or above its target; the others at or below their price.
            float gap =
                order.Kind == OrderKind.TakeProfitSell
                    ? (trigger - price) / price * 100f
                    : (price - trigger) / price * 100f;
            return Math.Max(0f, gap);
        }

        /// <summary>
        /// What happened lately, newest first: today's big moves, 30-day highs and lows,
        /// companies that began trading in the last <see cref="LatelyDays"/> days, and the
        /// session's notices (fills, expiries, bonds, company news).
        /// </summary>
        public static List<FeedItem> Lately(
            IEnumerable<MonitorQuote> quotes,
            IEnumerable<FeedEntry> log,
            IEnumerable<LocalCompany> companies,
            int today
        )
        {
            var items = new List<FeedItem>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q == null || q.Locked)
                {
                    continue;
                }

                if (!float.IsNaN(q.ChangeRate) && Math.Abs(q.ChangeRate) >= BigMovePercent)
                {
                    bool up = q.ChangeRate > 0f;
                    items.Add(
                        new FeedItem
                        {
                            Text = Strings.Format(
                                up ? "overview.feed.jumped" : "overview.feed.dropped",
                                q.Symbol,
                                StockMonitor.ChangeText(q.ChangeRate)
                            ),
                            When = Ago(0),
                            Symbol = q.Symbol,
                            Tone = up ? FeedTone.Gain : FeedTone.Loss,
                            Days = 0,
                            Rank = 0,
                        }
                    );
                }

                int extreme = HighOrLow(q);
                if (extreme != 0)
                {
                    items.Add(
                        new FeedItem
                        {
                            Text = Strings.Format(
                                extreme > 0 ? "overview.feed.high" : "overview.feed.low",
                                q.Symbol,
                                StockMonitor.PriceText(q.Price)
                            ),
                            When = Ago(0),
                            Symbol = q.Symbol,
                            Tone = extreme > 0 ? FeedTone.Gain : FeedTone.Loss,
                            Days = 0,
                            Rank = 1,
                        }
                    );
                }
            }

            foreach (LocalCompany c in companies ?? new List<LocalCompany>())
            {
                if (c == null || c.Status != CompanyStatus.Trading)
                {
                    continue;
                }

                int ago = today - c.FirstTradingDay;
                if (ago >= 0 && ago <= LatelyDays)
                {
                    items.Add(
                        new FeedItem
                        {
                            Text = Strings.Format("overview.feed.listed", c.Symbol, c.Name),
                            When = Ago(ago),
                            Symbol = c.Symbol,
                            Tone = FeedTone.Accent,
                            Days = ago,
                            Rank = 2,
                        }
                    );
                }
            }

            foreach (FeedEntry e in log ?? new List<FeedEntry>())
            {
                int ago = today - e.Day;
                // Offerings and first days come from the companies themselves (kept in the save).
                if (
                    ago < 0
                    || ago > LatelyDays
                    || e.Kind == NoticeKind.IpoAnnounced
                    || e.Kind == NoticeKind.IpoFirstDay
                    || e.Kind == NoticeKind.Summary
                )
                {
                    continue;
                }

                items.Add(
                    new FeedItem
                    {
                        Text = e.Text,
                        When = Ago(ago),
                        Symbol = e.Symbol,
                        Tone = e.Kind == NoticeKind.Filled ? FeedTone.Accent : FeedTone.Neutral,
                        Days = ago,
                        Rank = 3,
                    }
                );
            }

            // Newest first; on one day by rank, then in the order they were added.
            var keyed = new List<KeyValuePair<long, FeedItem>>();
            for (int i = 0; i < items.Count; i++)
            {
                long key = ((long)items[i].Days * 10 + items[i].Rank) * 100000 + i;
                keyed.Add(new KeyValuePair<long, FeedItem>(key, items[i]));
            }

            keyed.Sort((a, b) => a.Key.CompareTo(b.Key));
            items = keyed.ConvertAll(k => k.Value);
            return items;
        }

        /// <summary>
        /// +1 when today's price is above every other day of the last 30, −1 when below every
        /// other, else 0 (and 0 with fewer than <see cref="HighLowMinDays"/> days).
        /// </summary>
        public static int HighOrLow(MonitorQuote q)
        {
            float[] range = StockDetail.Range(q == null ? null : q.History, StockDetail.LongRange);
            var days = new List<float>();
            foreach (float v in range)
            {
                if (!float.IsNaN(v) && !float.IsInfinity(v))
                {
                    days.Add(v);
                }
            }

            if (days.Count < HighLowMinDays)
            {
                return 0;
            }

            float last = days[days.Count - 1];
            bool high = true,
                low = true;
            for (int i = 0; i < days.Count - 1; i++)
            {
                high &= last > days[i];
                low &= last < days[i];
            }

            return high ? 1
                : low ? -1
                : 0;
        }

        private static int Soonest(FeedItem a, FeedItem b)
        {
            int byDays = a.Days.CompareTo(b.Days);
            return byDays != 0 ? byDays : string.Compare(a.Text, b.Text, StringComparison.Ordinal);
        }

        /// <summary>"Today", "1 day ago", "5 days ago".</summary>
        public static string Ago(int days)
        {
            return days <= 0
                ? Strings.Get("overview.feed.when.today")
                : Strings.Plural(
                    "overview.feed.when.ago",
                    days,
                    days.ToString(CultureInfo.InvariantCulture)
                );
        }

        /// <summary>"Today", "in 1 day", "in 5 days".</summary>
        public static string InDays(int days)
        {
            return days <= 0
                ? Strings.Get("overview.feed.when.today")
                : Strings.Plural(
                    "overview.feed.when.in",
                    days,
                    days.ToString(CultureInfo.InvariantCulture)
                );
        }
    }
}
