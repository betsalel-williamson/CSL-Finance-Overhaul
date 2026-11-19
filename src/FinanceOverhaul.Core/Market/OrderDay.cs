using System; // Math, TimeSpan
using System.Collections.Generic; // IList

namespace FinanceOverhaul.Core
{
    /// <summary>What the game says about an order's stock today (the mod reads it, the rules decide).</summary>
    public struct ListingState
    {
        /// <summary>The stock exists in this city and is unlocked.</summary>
        public bool Listed;

        /// <summary>Today's price per share.</summary>
        public float Mark;

        /// <summary>Shares the exchange can still sell (vanilla <c>AvailableForBuyingCount</c>).</summary>
        public uint Available;

        /// <summary>Shares the city holds.</summary>
        public uint Owned;

        /// <summary>Ticker symbol for notices; the order's own key when the stock is not listed.</summary>
        public string Symbol;
    }

    public enum OrderStepKind
    {
        /// <summary>Nothing to do today.</summary>
        None,

        /// <summary>Buy <see cref="OrderStep.Shares"/> through the game, then <see cref="OrderDay.Filled"/>.</summary>
        Buy,

        /// <summary>Sell <see cref="OrderStep.Shares"/> through the game, then <see cref="OrderDay.Filled"/>.</summary>
        Sell,

        /// <summary>Take the order off the book (expired or cancelled) and post its notice.</summary>
        Remove,

        /// <summary>A trailing stop moved up: save the book.</summary>
        PeakRaised,
    }

    /// <summary>What the mod must do for one order today, with the notice and log line to go with it.</summary>
    public struct OrderStep
    {
        public OrderStepKind Kind;
        public uint Shares;

        /// <summary>For <see cref="OrderStepKind.Remove"/>: what to tell the player.</summary>
        public Notice Notice;

        /// <summary>For the mod's log; null when there is nothing to say.</summary>
        public string LogLine;

        /// <summary>The order changed or left the book, so the book must be saved.</summary>
        public bool BookChanged
        {
            get { return Kind != OrderStepKind.None; }
        }
    }

    /// <summary>
    /// One order's daily check, start to finish: whether it is looked at at all (frozen by a
    /// game UI test, its type switched off in Options, or its stock not listed yet), what
    /// <see cref="OrderRules"/> decides, and the notice and log line for the outcome. The mod
    /// supplies what it read from the game and carries out the step.
    /// </summary>
    public static class OrderDay
    {
        /// <summary>The absolute game day for the game clock's ticks.</summary>
        public static int DayOf(long gameTimeTicks)
        {
            return (int)(gameTimeTicks / TimeSpan.TicksPerDay);
        }

        /// <param name="cash">City cash in whole ₡.</param>
        /// <param name="kindEnabled">The order's type is switched on in Options.</param>
        /// <param name="frozen">A running game UI test froze this order (it is the player's).</param>
        public static OrderStep Step(
            TradeOrder order,
            int today,
            ListingState listing,
            long cash,
            bool kindEnabled,
            bool frozen
        )
        {
            var none = new OrderStep { Kind = OrderStepKind.None };
            if (order == null || frozen)
            {
                return none;
            }

            // Switched off in Options: kept as is (no fill, no expiry, no peak) until back on.
            if (!kindEnabled)
            {
                return none;
            }

            // A locked or missing listing just waits (until it expires).
            if (!listing.Listed && today <= order.ExpiresDay)
            {
                return none;
            }

            float mark = listing.Listed ? listing.Mark : 0f;
            OrderDecision d = OrderRules.Evaluate(
                order,
                today,
                mark,
                listing.Listed ? listing.Available : 0,
                cash,
                listing.Listed ? listing.Owned : 0
            );
            switch (d.Action)
            {
                case OrderAction.Fill:
                    return new OrderStep
                    {
                        Kind = order.IsSell ? OrderStepKind.Sell : OrderStepKind.Buy,
                        Shares = d.Shares,
                    };
                case OrderAction.Cancel:
                    string symbol = listing.Symbol ?? order.InvestmentKey;
                    return new OrderStep
                    {
                        Kind = OrderStepKind.Remove,
                        Notice =
                            d.Reason == OrderRules.ReasonExpired
                                ? NoticeText.Expired(
                                    order.Kind,
                                    order.Shares,
                                    symbol,
                                    order.ExpiresDay - order.PlacedDay
                                )
                                : NoticeText.Cancelled(
                                    order.Kind,
                                    order.Shares,
                                    symbol,
                                    d.Reason,
                                    (long)Math.Round((double)mark * order.Shares)
                                ),
                        LogLine = OrderLog.Cancelled(order, d.Reason),
                    };
                default:
                    return d.PeakRaised
                        ? new OrderStep
                        {
                            Kind = OrderStepKind.PeakRaised,
                            LogLine = OrderLog.PeakRaised(order, mark),
                        }
                        : none;
            }
        }

        /// <summary>
        /// After the game bought or sold the shares: the notice for the player and the log line.
        /// </summary>
        /// <param name="amount">Whole ₡ the game paid (buy) or received (sell).</param>
        public static OrderStep Filled(
            TradeOrder order,
            uint shares,
            ListingState listing,
            long amount
        )
        {
            return new OrderStep
            {
                Kind = order.IsSell ? OrderStepKind.Sell : OrderStepKind.Buy,
                Shares = shares,
                Notice = NoticeText.Filled(
                    order.Kind,
                    shares,
                    listing.Symbol ?? order.InvestmentKey,
                    listing.Mark,
                    order.TriggerPrice,
                    amount
                ),
                LogLine = OrderLog.Filled(order, shares, listing.Mark, amount),
            };
        }

        /// <summary>How many of these orders are for the Index.</summary>
        public static int CountForIndex(IList<TradeOrder> orders)
        {
            int n = 0;
            if (orders != null)
            {
                foreach (TradeOrder o in orders)
                {
                    if (o != null && CityIndexBasket.IsIndexName(o.InvestmentKey))
                    {
                        n++;
                    }
                }
            }

            return n;
        }
    }
}
