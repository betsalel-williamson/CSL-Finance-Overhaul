using System;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Player-facing wording and placement rules per order kind, shared by the ticket, the
    /// Your Portfolio card and the log. Prices are whole ₡ per share.
    /// </summary>
    public static class OrderText
    {
        /// <summary>The id suffix the string table uses for an order kind.</summary>
        public static string Key(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "stopLoss";
                case OrderKind.TakeProfitSell:
                    return "takeProfit";
                case OrderKind.TrailingStopSell:
                    return "trailingStop";
                default:
                    return "limit";
            }
        }

        public static string TabLabel(OrderKind kind)
        {
            return Strings.Get("order.tab." + Key(kind));
        }

        public static string ButtonLabel(OrderKind kind)
        {
            return Strings.Get("order.place." + Key(kind));
        }

        public static string PriceCaption(OrderKind kind)
        {
            return Strings.Get("order.caption." + Key(kind));
        }

        /// <summary>The estimate row, e.g. "Est. proceeds (at target): ₡1,200".</summary>
        public static string EstimateText(OrderKind kind, long amount)
        {
            return Strings.Format("order.estimate." + Key(kind), Money(amount));
        }

        /// <summary>The estimate row with why the order can't be placed after it.</summary>
        public static string EstimateText(OrderKind kind, long amount, string problem)
        {
            string estimate = EstimateText(kind, amount);
            return string.IsNullOrEmpty(problem)
                ? estimate
                : Strings.Format("order.estimate.problem", estimate, problem);
        }

        /// <param name="price">The field's value: ₡ per share, or the trail percent.</param>
        /// <param name="mark">Today's price (trailing stop: shows today's stop).</param>
        public static string Hint(
            OrderKind kind,
            int price,
            float mark = 0f,
            int lifetimeDays = TradeOrder.DefaultLifetimeDays
        )
        {
            int days = TradeOrder.ClampLifetime(lifetimeDays);
            string rule =
                kind == OrderKind.TrailingStopSell
                    ? Strings.Format(
                        "order.hint.trailingStop",
                        Money(price),
                        Money(TradeOrder.TrailingStop(mark, price))
                    )
                    : Strings.Format("order.hint." + Key(kind), Money(price));
            return Strings.Format(
                "join.sentences",
                rule,
                Strings.Plural("order.hint.goodFor", days, Whole(days))
            );
        }

        /// <summary>
        /// Pre-filled field: a limit at today's price, a stop 10% below it, a target 10%
        /// above it (whole ₡, at least 1); a trailing stop's trail of 10%.
        /// </summary>
        public static int SuggestedPrice(OrderKind kind, float mark)
        {
            if (kind == OrderKind.TrailingStopSell)
            {
                return 10;
            }

            if (float.IsNaN(mark) || float.IsInfinity(mark) || mark <= 0f)
            {
                return 1;
            }

            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return Math.Max(1, (int)Math.Floor(mark * 0.9));
                case OrderKind.TakeProfitSell:
                    // Round away float noise first: 100 × 1.1 must be 110, not 111.
                    return Math.Max(1, (int)Math.Ceiling(Math.Round(mark * 1.1, 4)));
                default:
                    return Math.Max(1, (int)Math.Floor(mark));
            }
        }

        /// <summary>Per-share price the estimate row uses: the field's price, or today's trailing stop.</summary>
        public static int EstimatePerShare(OrderKind kind, int price, float mark)
        {
            return kind == OrderKind.TrailingStopSell
                ? TradeOrder.TrailingStop(mark, price)
                : price;
        }

        /// <summary>
        /// Why this trigger can't be placed, or null: a sell stop at or above today's price, a
        /// sell target at or below it (a broker would reject or fill them at once), or a trail
        /// outside 1–50%.
        /// </summary>
        public static string PlacementProblem(OrderKind kind, int price, float mark)
        {
            if (
                kind == OrderKind.TrailingStopSell
                && (price < OrderRules.MinTrailPercent || price > OrderRules.MaxTrailPercent)
            )
            {
                return Strings.Format(
                    "order.problem.trail",
                    Whole(OrderRules.MinTrailPercent),
                    Whole(OrderRules.MaxTrailPercent)
                );
            }

            if (float.IsNaN(mark) || float.IsInfinity(mark) || mark <= 0f)
            {
                return null;
            }

            if (kind == OrderKind.StopLossSell && price >= mark)
            {
                return Strings.Format("order.problem.stop", Money((int)Math.Floor(mark)));
            }

            if (kind == OrderKind.TakeProfitSell && price <= mark)
            {
                return Strings.Format("order.problem.target", Money((int)Math.Floor(mark)));
            }

            return null;
        }

        /// <summary>Options dropdown entry for an order lifetime, e.g. "90 days".</summary>
        public static string LifetimeLabel(int days)
        {
            return Strings.Plural("days", days, Whole(days));
        }

        /// <summary>Options toggle label for an order type's feature flag.</summary>
        public static string OptionLabel(OrderKind kind)
        {
            return Strings.Get("options.flag." + Key(kind));
        }

        /// <summary>Side line of a card whose order type is switched off in Options.</summary>
        public static string CardSide(OrderKind kind, bool paused)
        {
            return paused ? Strings.Format("orders.paused", CardSide(kind)) : CardSide(kind);
        }

        /// <summary>The Your Portfolio card's side line.</summary>
        public static string CardSide(OrderKind kind)
        {
            return Strings.Get("order.card.side." + Key(kind));
        }

        /// <summary>The Your Portfolio card's detail line, e.g. "10 shares if the price rises to ₡120 · 5 days left".</summary>
        public static string CardDetail(OrderKind kind, uint shares, int price, int daysLeft)
        {
            string id =
                kind == OrderKind.StopLossSell || kind == OrderKind.TakeProfitSell
                    ? "order.card." + Key(kind)
                    : "order.card.limit";
            return WithDaysLeft(Strings.Plural(id, shares, Money(shares), Money(price)), daysLeft);
        }

        /// <summary>The Your Portfolio card's detail line for an open order.</summary>
        public static string CardDetail(TradeOrder order, int today)
        {
            int daysLeft = order.ExpiresDay - today;
            if (order.Kind != OrderKind.TrailingStopSell)
            {
                return CardDetail(order.Kind, order.Shares, order.TriggerPrice, daysLeft);
            }

            return WithDaysLeft(
                Strings.Plural(
                    "order.card.trailingStop",
                    order.Shares,
                    Money(order.Shares),
                    Whole(order.TrailPercent),
                    Money(order.TriggerPrice)
                ),
                daysLeft
            );
        }

        private static string WithDaysLeft(string what, int daysLeft)
        {
            int left = Math.Max(0, daysLeft);
            return Strings.Format(
                "order.card.detail",
                what,
                Strings.Plural("daysLeft", left, Whole(left))
            );
        }

        private static string Whole(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Money(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
