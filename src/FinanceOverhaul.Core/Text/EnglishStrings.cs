using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The built-in English wording, one entry per id. Ids are grouped by where the text
    /// shows; "{0}" marks a value filled in by the code. Plural entries end ".one" / ".other".
    /// </summary>
    internal static class EnglishStrings
    {
        internal static readonly Dictionary<string, string> Table = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            // Shared units
            { "shares.one", "{0} share" },
            { "shares.other", "{0} shares" },
            { "daysLeft.one", "{0} day left" },
            { "daysLeft.other", "{0} days left" },
            { "daysAgo.one", "{0} day ago" },
            { "daysAgo.other", "{0} days ago" },
            // Stock Monitor: window
            { "monitor.title", "Stock Monitor" },
            { "monitor.button.tip", "Stock Monitor (right-drag to move)" },
            { "monitor.settings.tip", "Stock Monitor settings" },
            { "monitor.pill.tip", "Click: show the change in ₡ or %" },
            { "monitor.footer", "Daily close · {0}" },
            // Stock Monitor: tabs
            { "monitor.filter.all", "All" },
            { "monitor.filter.portfolio", "Portfolio" },
            { "monitor.filter.orders", "Orders" },
            { "monitor.filter.ordersCount", "Orders ({0})" },
            { "monitor.empty.all", "No stocks are listed yet." },
            { "monitor.empty.portfolio", "The city holds no shares yet." },
            { "monitor.empty.orders", "No open orders." },
            // Stock Monitor: sort
            { "monitor.sort.change", "Sort: Change %" },
            { "monitor.sort.changeCurrency", "Sort: Change ₡" },
            { "monitor.sort.symbol", "Sort: Symbol" },
            { "monitor.sort.price", "Sort: Price" },
            { "monitor.sort.valueHeld", "Sort: Value held" },
            { "monitor.sort.nearest", "Sort: Nearest" },
            { "monitor.sort.expires", "Sort: Expires" },
            { "monitor.direction.flip", "{0} (click to flip)" },
            { "monitor.direction.highest", "Highest first" },
            { "monitor.direction.lowest", "Lowest first" },
            { "monitor.direction.az", "A → Z" },
            { "monitor.direction.za", "Z → A" },
            { "monitor.direction.nearest", "Nearest first" },
            { "monitor.direction.farthest", "Farthest first" },
            { "monitor.direction.soonest", "Soonest first" },
            { "monitor.direction.latest", "Latest first" },
            // Stock Monitor: rows
            { "monitor.badge.one", "{0} order" },
            { "monitor.badge.other", "{0} orders" },
            { "monitor.hover", "{0} · {1}" },
            { "monitor.hover.symbol", "{0} · {1} · {2}" },
            { "monitor.hover.today", "today" },
            { "monitor.hover.yesterday", "yesterday" },
            // Stock Monitor: order lines
            { "orders.type.limitBuy", "BUY · Limit" },
            { "orders.type.stopLoss", "SELL · Stop-loss" },
            { "orders.type.takeProfit", "SELL · Take-profit" },
            { "orders.type.trailingStop", "SELL · Trailing stop" },
            { "orders.paused", "{0} · paused in Options" },
            { "orders.away", "₡{0} away" },
            { "orders.awayPercent", "{0} · {1}%" },
            { "orders.atTrigger", "at the trigger" },
            { "orders.short.limitBuy", "BUY ≤ ₡{0}" },
            { "orders.short.stopLoss", "SELL ≤ ₡{0} (stop)" },
            { "orders.short.takeProfit", "SELL ≥ ₡{0} (target)" },
            { "orders.short.trailingStop", "SELL, {0}% trail" },
            { "orders.compact.trailingStop", "SELL, {0}% trail · stop ₡{1}" },
            { "orders.compact", "{0} · {1} · {2}" },
            { "orders.summary.many", "{0} open (see Your Portfolio)" },
            // Stock Monitor: detail view
            { "detail.today", "Today" },
            { "detail.todayYesterday", "Today · yesterday {0}" },
            { "detail.yesterday", "Yesterday" },
            { "detail.high", "{0}-day high" },
            { "detail.low", "{0}-day low" },
            { "detail.gain", "Gain" },
            { "detail.loss", "Loss" },
            { "detail.noShares", "none" },
            { "detail.youOwn", "You own" },
            { "detail.avgCost", "Avg cost / share" },
            { "detail.currentValue", "Current value" },
            { "detail.openOrders", "Open orders" },
            { "detail.buy", "Buy" },
            { "detail.sell", "Sell" },
            { "detail.trade.tip", "Buy / Sell open the game's Invest dialog (with order tabs)" },
            { "detail.sell.none", "The city holds no shares of {0}" },
            { "detail.history.limit", "the game keeps 30 daily prices" },
            // Stock Monitor: settings
            { "monitor.settings.title", "Stock Monitor settings" },
            { "monitor.settings.opacity", "Window opacity" },
            { "monitor.settings.opacity.note", "Text and charts fade with the background" },
            { "monitor.settings.solidOnHover", "Solid while the mouse is over it" },
            { "monitor.settings.compact", "Compact rows (hide names)" },
            { "monitor.settings.sparklines", "Show sparklines" },
            { "monitor.settings.changeCurrency", "Change as ₡ instead of %" },
            { "monitor.settings.stocks", "Stocks shown" },
            { "monitor.settings.note", "Kept per player (settings file), not in the city save." },
        };
    }
}
