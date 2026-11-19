using System; // Math, StringComparison
using System.Collections.Generic; // IList, List
using System.Globalization; // CultureInfo, NumberStyles
using System.Text; // StringBuilder

namespace FinanceOverhaul.Core
{
    public enum OrderKind
    {
        /// <summary>Buy when the daily price is at or below the trigger.</summary>
        LimitBuy,

        /// <summary>Sell when the daily price is at or below the trigger (stop-loss).</summary>
        StopLossSell,

        /// <summary>Sell when the daily price is at or above the trigger (take-profit).</summary>
        TakeProfitSell,

        /// <summary>
        /// Sell when the daily price falls a set percent below its highest daily price since
        /// placing (trailing stop). The stop only moves up.
        /// </summary>
        TrailingStopSell,
    }

    /// <summary>
    /// One open order. Prices are whole ₡ per share (as the game shows them); days are
    /// absolute game-day numbers. <see cref="InvestmentKey"/> is the investment's prefab
    /// name, stable across saves.
    /// </summary>
    public sealed class TradeOrder
    {
        /// <summary>
        /// Good-'til-cancelled cap, like Schwab's: by default orders expire after this many game
        /// days. The player picks the lifetime in Options (<see cref="LifetimeChoices"/>); each
        /// order keeps the expiry it was placed with.
        /// </summary>
        public const int DefaultLifetimeDays = 90;

        /// <summary>The Options choices, in game days.</summary>
        public static readonly int[] LifetimeChoices = { 30, 60, 90, 120, 180, 365 };

        /// <summary>A usable lifetime: 1–3650 days, else the default.</summary>
        public static int ClampLifetime(int days)
        {
            return days >= 1 && days <= 3650 ? days : DefaultLifetimeDays;
        }

        private TradeOrder() { }

        public int Id { get; private set; }

        public OrderKind Kind { get; private set; }

        public string InvestmentKey { get; private set; }

        public uint Shares { get; private set; }

        private int triggerPrice;

        /// <summary>
        /// The limit (buys), stop or target (sells), whole ₡ per share. For a trailing stop,
        /// today's stop: <see cref="TrailPercent"/> below <see cref="Peak"/>, rounded down.
        /// </summary>
        public int TriggerPrice
        {
            get
            {
                return Kind == OrderKind.TrailingStopSell
                    ? TrailingStop(Peak, TrailPercent)
                    : triggerPrice;
            }
        }

        /// <summary>Trailing stop only: whole percent below the peak (1–50).</summary>
        public int TrailPercent { get; private set; }

        /// <summary>Trailing stop only: the highest daily price since placing.</summary>
        public float Peak { get; private set; }

        /// <summary>Trailing stop only: follow a new high. Returns true when the peak rose.</summary>
        public bool RaisePeak(float mark)
        {
            if (
                Kind != OrderKind.TrailingStopSell
                || float.IsNaN(mark)
                || float.IsInfinity(mark)
                || mark <= Peak
            )
            {
                return false;
            }

            Peak = mark;
            return true;
        }

        /// <summary>The stop <paramref name="percent"/> below <paramref name="peak"/>, whole ₡ (rounded down).</summary>
        public static int TrailingStop(float peak, int percent)
        {
            if (float.IsNaN(peak) || float.IsInfinity(peak) || peak <= 0f)
            {
                return 0;
            }

            // Round away float noise first: 100 × 90% must be 90, not 89.
            return (int)Math.Floor(Math.Round((double)peak * (100 - percent) / 100.0, 4));
        }

        public bool IsSell
        {
            get { return Kind != OrderKind.LimitBuy; }
        }

        public int PlacedDay { get; private set; }

        public int ExpiresDay { get; private set; }

        public static TradeOrder LimitBuy(
            int id,
            string investmentKey,
            uint shares,
            int limitPrice,
            int placedDay,
            int lifetimeDays = DefaultLifetimeDays
        )
        {
            return Create(
                OrderKind.LimitBuy,
                id,
                investmentKey,
                shares,
                limitPrice,
                placedDay,
                placedDay + ClampLifetime(lifetimeDays)
            );
        }

        public static TradeOrder StopLossSell(
            int id,
            string investmentKey,
            uint shares,
            int stopPrice,
            int placedDay,
            int lifetimeDays = DefaultLifetimeDays
        )
        {
            return Create(
                OrderKind.StopLossSell,
                id,
                investmentKey,
                shares,
                stopPrice,
                placedDay,
                placedDay + ClampLifetime(lifetimeDays)
            );
        }

        public static TradeOrder TakeProfitSell(
            int id,
            string investmentKey,
            uint shares,
            int targetPrice,
            int placedDay,
            int lifetimeDays = DefaultLifetimeDays
        )
        {
            return Create(
                OrderKind.TakeProfitSell,
                id,
                investmentKey,
                shares,
                targetPrice,
                placedDay,
                placedDay + ClampLifetime(lifetimeDays)
            );
        }

        public static TradeOrder TrailingStopSell(
            int id,
            string investmentKey,
            uint shares,
            int trailPercent,
            float peak,
            int placedDay,
            int lifetimeDays = DefaultLifetimeDays
        )
        {
            TradeOrder order = Create(
                OrderKind.TrailingStopSell,
                id,
                investmentKey,
                shares,
                0,
                placedDay,
                placedDay + ClampLifetime(lifetimeDays)
            );
            order.TrailPercent = trailPercent;
            order.Peak = peak;
            return order;
        }

        internal static TradeOrder Restore(
            OrderKind kind,
            int id,
            string investmentKey,
            uint shares,
            int trigger,
            int placedDay,
            int expiresDay,
            int trailPercent,
            float peak
        )
        {
            TradeOrder order = Create(
                kind,
                id,
                investmentKey,
                shares,
                trigger,
                placedDay,
                expiresDay
            );
            order.TrailPercent = trailPercent;
            order.Peak = peak;
            return order;
        }

        internal static TradeOrder Create(
            OrderKind kind,
            int id,
            string investmentKey,
            uint shares,
            int limitPrice,
            int placedDay,
            int expiresDay
        )
        {
            return new TradeOrder
            {
                Id = id,
                Kind = kind,
                InvestmentKey = investmentKey,
                Shares = shares,
                triggerPrice = limitPrice,
                PlacedDay = placedDay,
                ExpiresDay = expiresDay,
            };
        }
    }

    public enum OrderAction
    {
        Wait,
        Fill,
        Cancel,
    }

    public struct OrderDecision
    {
        public OrderAction Action;
        public uint Shares;

        /// <summary>Whole ₡, as vanilla <c>CalculatePrice</c> returns.</summary>
        public int Cost;
        public string Reason;

        /// <summary>Trailing stop: the peak moved up (the book must be saved).</summary>
        public bool PeakRaised;
    }

    /// <summary>
    /// Daily order checks, run right after the vanilla daily investment update (the
    /// game's "daily close"). Mirrors vanilla pricing: cost = round(mark × shares).
    /// </summary>
    public static class OrderRules
    {
        public const string ReasonExpired = "expired";
        public const string ReasonFunds = "insufficient funds";
        public const string ReasonShareLimit = "share limit";
        public const string ReasonNoShares = "no shares left to sell";

        /// <summary>Trailing stop bounds, whole percent below the peak.</summary>
        public const int MinTrailPercent = 1;
        public const int MaxTrailPercent = 50;

        /// <param name="today">Absolute game day.</param>
        /// <param name="mark">The investment's value per share today.</param>
        /// <param name="availableShares">Vanilla <c>AvailableForBuyingCount</c>.</param>
        /// <param name="cash">City cash in whole ₡ (vanilla <c>EconomyManager.playerMoney</c>).</param>
        /// <param name="owned">Shares the city holds (sell orders).</param>
        public static OrderDecision Evaluate(
            TradeOrder order,
            int today,
            float mark,
            uint availableShares,
            long cash,
            uint owned = 0
        )
        {
            if (today > order.ExpiresDay)
            {
                return Cancel(ReasonExpired);
            }

            if (order.Kind == OrderKind.StopLossSell)
            {
                return EvaluateStopLoss(order, mark, owned);
            }

            if (order.Kind == OrderKind.TakeProfitSell)
            {
                return EvaluateTakeProfit(order, mark, owned);
            }

            if (order.Kind == OrderKind.TrailingStopSell)
            {
                return EvaluateTrailingStop(order, mark, owned);
            }

            if (
                float.IsNaN(mark)
                || float.IsInfinity(mark)
                || mark <= 0f
                || mark > order.TriggerPrice
            )
            {
                return new OrderDecision { Action = OrderAction.Wait };
            }

            uint shares = Math.Min(order.Shares, availableShares);
            if (shares == 0)
            {
                return Cancel(ReasonShareLimit);
            }

            int cost = Price(mark, shares);
            if (cost > cash)
            {
                return Cancel(ReasonFunds);
            }

            return new OrderDecision
            {
                Action = OrderAction.Fill,
                Shares = shares,
                Cost = cost,
            };
        }

        /// <summary>
        /// Stop-loss: once the daily price is at or below the stop, sell at that day's price
        /// (lower than the stop after a gap down), never more than is owned.
        /// </summary>
        private static OrderDecision EvaluateStopLoss(TradeOrder order, float mark, uint owned)
        {
            if (
                float.IsNaN(mark)
                || float.IsInfinity(mark)
                || mark <= 0f
                || mark > order.TriggerPrice
            )
            {
                return new OrderDecision { Action = OrderAction.Wait };
            }

            return SellOwned(order, mark, owned);
        }

        /// <summary>
        /// Take-profit (a sell limit): once the daily price is at or above the target, sell
        /// at that day's price (higher than the target after a gap up), never more than is
        /// owned.
        /// </summary>
        private static OrderDecision EvaluateTakeProfit(TradeOrder order, float mark, uint owned)
        {
            if (
                float.IsNaN(mark)
                || float.IsInfinity(mark)
                || mark <= 0f
                || mark < order.TriggerPrice
            )
            {
                return new OrderDecision { Action = OrderAction.Wait };
            }

            return SellOwned(order, mark, owned);
        }

        /// <summary>
        /// Trailing stop: a new high raises the peak (and so the stop); once the daily price
        /// is at or below the stop, sell at that day's price, never more than is owned.
        /// Note: this updates <see cref="TradeOrder.Peak"/> on the order.
        /// </summary>
        private static OrderDecision EvaluateTrailingStop(TradeOrder order, float mark, uint owned)
        {
            if (float.IsNaN(mark) || float.IsInfinity(mark) || mark <= 0f)
            {
                return new OrderDecision { Action = OrderAction.Wait };
            }

            bool raised = order.RaisePeak(mark);
            if (mark > order.TriggerPrice)
            {
                return new OrderDecision { Action = OrderAction.Wait, PeakRaised = raised };
            }

            OrderDecision d = SellOwned(order, mark, owned);
            d.PeakRaised = raised;
            return d;
        }

        private static OrderDecision SellOwned(TradeOrder order, float mark, uint owned)
        {
            uint shares = Math.Min(order.Shares, owned);
            if (shares == 0)
            {
                return Cancel(ReasonNoShares);
            }

            return new OrderDecision
            {
                Action = OrderAction.Fill,
                Shares = shares,
                Cost = Price(mark, shares),
            };
        }

        /// <summary>Same rounding as vanilla <c>InvestmentAI.CalculatePrice</c> (Mathf.RoundToInt).</summary>
        public static int Price(float mark, uint shares)
        {
            return (int)Math.Round((double)(mark * shares));
        }

        private static OrderDecision Cancel(string reason)
        {
            return new OrderDecision { Action = OrderAction.Cancel, Reason = reason };
        }
    }

    /// <summary>The city's open orders. Ids increase and are never reused.</summary>
    public sealed class OrderBook
    {
        private readonly List<TradeOrder> orders = new List<TradeOrder>();

        public int NextId { get; internal set; } = 1;

        private int lifetimeDays = TradeOrder.DefaultLifetimeDays;

        /// <summary>Lifetime given to orders placed from now on (Options); existing orders keep theirs.</summary>
        public int LifetimeDays
        {
            get { return lifetimeDays; }
            set { lifetimeDays = TradeOrder.ClampLifetime(value); }
        }

        public IList<TradeOrder> Orders
        {
            get { return orders.AsReadOnly(); }
        }

        /// <summary>Returns the new order, or null when it would be meaningless.</summary>
        public TradeOrder AddLimitBuy(string investmentKey, uint shares, int limitPrice, int today)
        {
            if (string.IsNullOrEmpty(investmentKey) || shares == 0 || limitPrice <= 0)
            {
                return null;
            }

            TradeOrder order = TradeOrder.LimitBuy(
                NextId++,
                investmentKey,
                shares,
                limitPrice,
                today,
                LifetimeDays
            );
            orders.Add(order);
            return order;
        }

        /// <summary>
        /// Places an order of any kind; null when it would be meaningless.
        /// </summary>
        /// <param name="trigger">Whole ₡ per share, or the trail percent for a trailing stop.</param>
        /// <param name="mark">Today's price (a trailing stop starts its peak there).</param>
        public TradeOrder Place(
            OrderKind kind,
            string investmentKey,
            uint shares,
            int trigger,
            float mark,
            int today
        )
        {
            switch (kind)
            {
                case OrderKind.LimitBuy:
                    return AddLimitBuy(investmentKey, shares, trigger, today);
                case OrderKind.TakeProfitSell:
                    return AddTakeProfit(investmentKey, shares, trigger, today);
                case OrderKind.TrailingStopSell:
                    return AddTrailingStop(investmentKey, shares, trigger, mark, today);
                default:
                    return AddStopLoss(investmentKey, shares, trigger, today);
            }
        }

        /// <summary>Stop-loss sell; null when it would be meaningless.</summary>
        public TradeOrder AddStopLoss(string investmentKey, uint shares, int stopPrice, int today)
        {
            return AddSell(OrderKind.StopLossSell, investmentKey, shares, stopPrice, today);
        }

        /// <summary>Take-profit sell; null when it would be meaningless.</summary>
        public TradeOrder AddTakeProfit(
            string investmentKey,
            uint shares,
            int targetPrice,
            int today
        )
        {
            return AddSell(OrderKind.TakeProfitSell, investmentKey, shares, targetPrice, today);
        }

        /// <summary>
        /// Trailing stop sell starting from today's price as the peak; null when it would
        /// be meaningless (trail outside 1–50%, no price).
        /// </summary>
        public TradeOrder AddTrailingStop(
            string investmentKey,
            uint shares,
            int trailPercent,
            float mark,
            int today
        )
        {
            if (
                string.IsNullOrEmpty(investmentKey)
                || shares == 0
                || trailPercent < OrderRules.MinTrailPercent
                || trailPercent > OrderRules.MaxTrailPercent
                || float.IsNaN(mark)
                || float.IsInfinity(mark)
                || mark <= 0f
            )
            {
                return null;
            }

            TradeOrder order = TradeOrder.TrailingStopSell(
                NextId++,
                investmentKey,
                shares,
                trailPercent,
                mark,
                today,
                LifetimeDays
            );
            orders.Add(order);
            return order;
        }

        private TradeOrder AddSell(
            OrderKind kind,
            string investmentKey,
            uint shares,
            int trigger,
            int today
        )
        {
            if (string.IsNullOrEmpty(investmentKey) || shares == 0 || trigger <= 0)
            {
                return null;
            }

            TradeOrder order = TradeOrder.Create(
                kind,
                NextId++,
                investmentKey,
                shares,
                trigger,
                today,
                today + LifetimeDays
            );
            orders.Add(order);
            return order;
        }

        public bool Cancel(int id)
        {
            int i = orders.FindIndex(o => o.Id == id);
            if (i < 0)
            {
                return false;
            }

            orders.RemoveAt(i);
            return true;
        }

        internal void Restore(TradeOrder order)
        {
            orders.Add(order);
            NextId = Math.Max(NextId, order.Id + 1);
        }
    }

    /// <summary>
    /// Versioned text format for the order book in the save (mod data id
    /// <see cref="DataId"/>). Unknown versions, unknown kinds and malformed lines are
    /// skipped, so a newer or damaged blob never breaks loading.
    /// </summary>
    public static class OrderBookCodec
    {
        public const string DataId = "FinanceOverhaul.Orders";
        private const string Version = "v1";
        private const string LimitBuyTag = "limit-buy";
        private const string StopLossTag = "stop-sell";
        private const string TakeProfitTag = "profit-sell";

        // Trailing stops carry the trail percent in the trigger column and an 8th column,
        // the peak; older builds only read 7-column lines, so they skip them.
        private const string TrailingStopTag = "trail-sell";

        private static string Tag(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return StopLossTag;
                case OrderKind.TakeProfitSell:
                    return TakeProfitTag;
                case OrderKind.TrailingStopSell:
                    return TrailingStopTag;
                default:
                    return LimitBuyTag;
            }
        }

        public static string Format(OrderBook book)
        {
            var sb = new StringBuilder();
            sb.Append(Version).Append('\n');
            sb.Append("next=")
                .Append(book.NextId.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            foreach (TradeOrder o in book.Orders)
            {
                sb.Append(Tag(o.Kind))
                    .Append('|')
                    .Append(o.Id.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(Escape(o.InvestmentKey))
                    .Append('|')
                    .Append(o.Shares.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(
                        (
                            o.Kind == OrderKind.TrailingStopSell ? o.TrailPercent : o.TriggerPrice
                        ).ToString(CultureInfo.InvariantCulture)
                    )
                    .Append('|')
                    .Append(o.PlacedDay.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(o.ExpiresDay.ToString(CultureInfo.InvariantCulture));
                if (o.Kind == OrderKind.TrailingStopSell)
                {
                    sb.Append('|').Append(o.Peak.ToString("R", CultureInfo.InvariantCulture));
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        public static OrderBook Parse(string text)
        {
            var book = new OrderBook();
            if (string.IsNullOrEmpty(text))
            {
                return book;
            }

            string[] lines = text.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Version)
            {
                return book;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r');
                if (line.StartsWith("next=", StringComparison.Ordinal))
                {
                    int next;
                    if (TryInt(line.Substring(5), out next) && next > 0)
                    {
                        book.NextId = Math.Max(book.NextId, next);
                    }

                    continue;
                }

                string[] f = line.Split('|');
                OrderKind kind;
                float peak = 0f;
                if (f.Length == 8 && f[0] == TrailingStopTag)
                {
                    kind = OrderKind.TrailingStopSell;
                    if (
                        !float.TryParse(
                            f[7],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out peak
                        )
                        || float.IsNaN(peak)
                        || float.IsInfinity(peak)
                        || peak <= 0f
                    )
                    {
                        continue;
                    }
                }
                else if (f.Length != 7)
                {
                    continue;
                }
                else if (f[0] == LimitBuyTag)
                {
                    kind = OrderKind.LimitBuy;
                }
                else if (f[0] == StopLossTag)
                {
                    kind = OrderKind.StopLossSell;
                }
                else if (f[0] == TakeProfitTag)
                {
                    kind = OrderKind.TakeProfitSell;
                }
                else
                {
                    continue; // a kind from a newer build: skipped, not an error
                }

                int id,
                    limit,
                    placed,
                    expires;
                uint shares;
                string key = Unescape(f[2]);
                if (
                    TryInt(f[1], out id)
                    && uint.TryParse(
                        f[3],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out shares
                    )
                    && TryInt(f[4], out limit)
                    && TryInt(f[5], out placed)
                    && TryInt(f[6], out expires)
                    && id > 0
                    && shares > 0
                    && limit > 0
                    && key.Length > 0
                )
                {
                    if (kind == OrderKind.TrailingStopSell)
                    {
                        if (
                            limit < OrderRules.MinTrailPercent
                            || limit > OrderRules.MaxTrailPercent
                        )
                        {
                            continue;
                        }

                        book.Restore(
                            TradeOrder.Restore(
                                kind,
                                id,
                                key,
                                shares,
                                0,
                                placed,
                                expires,
                                limit,
                                peak
                            )
                        );
                    }
                    else
                    {
                        book.Restore(
                            TradeOrder.Create(kind, id, key, shares, limit, placed, expires)
                        );
                    }
                }
            }

            return book;
        }

        private static bool TryInt(string s, out int value)
        {
            return int.TryParse(
                s,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value
            );
        }

        private static string Escape(string s)
        {
            return s.Replace("%", "%25")
                .Replace("|", "%7C")
                .Replace("\n", "%0A")
                .Replace("\r", "%0D");
        }

        private static string Unescape(string s)
        {
            return s.Replace("%0D", "\r")
                .Replace("%0A", "\n")
                .Replace("%7C", "|")
                .Replace("%25", "%");
        }
    }
}
