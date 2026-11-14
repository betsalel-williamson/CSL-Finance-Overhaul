using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FinanceOverhaul.Market
{
    public enum OrderKind
    {
        /// <summary>Buy when the daily price is at or below the trigger.</summary>
        LimitBuy,

        /// <summary>Sell when the daily price is at or below the trigger (stop-loss).</summary>
        StopLossSell,
    }

    /// <summary>
    /// One open order. Prices are whole ₡ per share (as the game shows them); days are
    /// absolute game-day numbers. <see cref="InvestmentKey"/> is the investment's prefab
    /// name, stable across saves.
    /// </summary>
    public sealed class TradeOrder
    {
        /// <summary>Good-'til-cancelled cap, like Schwab's: orders expire after this many game days.</summary>
        public const int LifetimeDays = 60;

        private TradeOrder() { }

        public int Id { get; private set; }

        public OrderKind Kind { get; private set; }

        public string InvestmentKey { get; private set; }

        public uint Shares { get; private set; }

        /// <summary>The limit (buys) or stop (sells), whole ₡ per share.</summary>
        public int TriggerPrice { get; private set; }

        public bool IsSell
        {
            get { return Kind == OrderKind.StopLossSell; }
        }

        public int PlacedDay { get; private set; }

        public int ExpiresDay { get; private set; }

        public static TradeOrder LimitBuy(
            int id,
            string investmentKey,
            uint shares,
            int limitPrice,
            int placedDay
        )
        {
            return Create(
                OrderKind.LimitBuy,
                id,
                investmentKey,
                shares,
                limitPrice,
                placedDay,
                placedDay + LifetimeDays
            );
        }

        public static TradeOrder StopLossSell(
            int id,
            string investmentKey,
            uint shares,
            int stopPrice,
            int placedDay
        )
        {
            return Create(
                OrderKind.StopLossSell,
                id,
                investmentKey,
                shares,
                stopPrice,
                placedDay,
                placedDay + LifetimeDays
            );
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
                TriggerPrice = limitPrice,
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
                today
            );
            orders.Add(order);
            return order;
        }

        /// <summary>Stop-loss sell; null when it would be meaningless.</summary>
        public TradeOrder AddStopLoss(string investmentKey, uint shares, int stopPrice, int today)
        {
            if (string.IsNullOrEmpty(investmentKey) || shares == 0 || stopPrice <= 0)
            {
                return null;
            }

            TradeOrder order = TradeOrder.StopLossSell(
                NextId++,
                investmentKey,
                shares,
                stopPrice,
                today
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

        private static string Tag(OrderKind kind)
        {
            return kind == OrderKind.StopLossSell ? StopLossTag : LimitBuyTag;
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
                    .Append(o.TriggerPrice.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(o.PlacedDay.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(o.ExpiresDay.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
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
                if (f.Length != 7)
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
                    book.Restore(TradeOrder.Create(kind, id, key, shares, limit, placed, expires));
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
