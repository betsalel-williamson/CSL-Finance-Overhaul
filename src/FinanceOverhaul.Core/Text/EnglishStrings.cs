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
            { "days.one", "{0} day" },
            { "days.other", "{0} days" },
            // Joining: two whole sentences, and items in a list.
            { "join.sentences", "{0} {1}" },
            { "join.list", "{0}, {1}" },
            // The thousands separator put into a number while the player types it.
            { "number.group", "," },
            // Price charts (Stock Monitor detail, the Invest dialog, the portfolio tooltip): the
            // right-hand end of the time axis. The left-hand end is "daysAgo".
            { "chart.today", "today" },
            // Stock Monitor: window
            { "monitor.title", "Stock Monitor" },
            { "monitor.button.tip", "Stock Monitor (right-drag to move)" },
            { "monitor.settings.tip", "Stock Monitor settings" },
            { "monitor.pill.tip", "Click: show the change in ₡ or %" },
            { "monitor.footer", "Daily close · {0}" },
            // The footer's date, as a .NET date pattern (day, short month name, year). Month
            // names stay English.
            { "monitor.footer.date", "d MMM yyyy" },
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
            { "detail.back", "« All stocks" },
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
            // Stock Exchange LED ticker: the boards draw Latin letters, digits and ₡ only.
            { "ticker.waiting", "Waiting for market data" },
            // More than one copy of the mod switched on. The game's Yes / No dialog shows about
            // five short lines, so these stay brief.
            { "duplicate.title", "Finance Overhaul is switched on twice" },
            { "duplicate.copy.workshop", "{0} (Workshop)" },
            { "duplicate.copy.local", "{0} (local)" },
            { "duplicate.copy.unnamed", "unknown version" },
            {
                "duplicate.ask.two",
                "Two copies are switched on and both would act on your city.\n\nYes: keep {0}\nNo: keep {1}"
            },
            {
                "duplicate.ask.many",
                "{0} copies are switched on and all would act on your city.\n\nYes: keep only {1}\nNo: leave them all on"
            },
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
            // Order ticket (the game's Invest dialog)
            { "order.tab.buyNow", "Buy now" },
            { "order.tab.sellNow", "Sell now" },
            { "order.tab.limit", "Limit" },
            { "order.tab.stopLoss", "Stop-loss" },
            { "order.tab.takeProfit", "Take-profit" },
            { "order.tab.trailingStop", "Trailing stop" },
            { "order.place.limit", "Place limit order" },
            { "order.place.stopLoss", "Place stop-loss" },
            { "order.place.takeProfit", "Place take-profit" },
            { "order.place.trailingStop", "Place trailing stop" },
            { "order.caption.limit", "Limit price (₡ per share)" },
            { "order.caption.stopLoss", "Stop price (₡ per share)" },
            { "order.caption.takeProfit", "Target price (₡ per share)" },
            { "order.caption.trailingStop", "Trail (% below the peak)" },
            { "order.estimate.limit", "Est. cost (at limit): ₡{0}" },
            { "order.estimate.stopLoss", "Est. proceeds (at stop): ₡{0}" },
            { "order.estimate.takeProfit", "Est. proceeds (at target): ₡{0}" },
            { "order.estimate.trailingStop", "Est. proceeds (at today's stop): ₡{0}" },
            { "order.estimate.problem", "{0} — {1}" },
            { "order.hint.limit", "Buys when the daily price is at or below ₡{0}." },
            {
                "order.hint.stopLoss",
                "Sells when the daily price is at or below ₡{0}. After a sharp drop it can sell for less."
            },
            {
                "order.hint.takeProfit",
                "Sells when the daily price is at or above ₡{0}. After a sharp rise it can sell for more."
            },
            {
                "order.hint.trailingStop",
                "Sells if the daily price falls {0}% below its highest since you place it (stop now ₡{1}). The stop only moves up."
            },
            { "order.hint.goodFor.one", "Good for {0} day." },
            { "order.hint.goodFor.other", "Good for {0} days." },
            { "order.problem.trail", "Set the trail between {0}% and {1}%." },
            { "order.problem.stop", "Set the stop below today's price (₡{0})." },
            { "order.problem.target", "Set the target above today's price (₡{0})." },
            { "order.problem.afford", "The city can't afford this many shares at your limit." },
            { "order.problem.failed", "The order could not be placed." },
            { "order.enter.price", "Enter a price in whole ₡." },
            { "order.enter.trail", "Enter the trail in whole percent." },
            { "order.enter.priceShares", "Enter a price in whole ₡ and at least one share." },
            {
                "order.enter.trailShares",
                "Enter the trail in whole percent and at least one share."
            },
            { "order.price.tip", "Whole ₡ per share · orders trade at the daily price" },
            { "order.shares.tip", "Type a share amount · Enter/Tab apply · Esc reverts" },
            // Your Portfolio: open order cards
            { "order.card.side.limit", "Open order · BUY" },
            { "order.card.side.stopLoss", "Open order · SELL (stop-loss)" },
            { "order.card.side.takeProfit", "Open order · SELL (take-profit)" },
            { "order.card.side.trailingStop", "Open order · SELL (trailing stop)" },
            { "order.card.limit.one", "{0} share at or below ₡{1}" },
            { "order.card.limit.other", "{0} shares at or below ₡{1}" },
            { "order.card.stopLoss.one", "{0} share if the price drops to ₡{1}" },
            { "order.card.stopLoss.other", "{0} shares if the price drops to ₡{1}" },
            { "order.card.takeProfit.one", "{0} share if the price rises to ₡{1}" },
            { "order.card.takeProfit.other", "{0} shares if the price rises to ₡{1}" },
            { "order.card.trailingStop.one", "{0} share, {1}% trail, stop ₡{2}" },
            { "order.card.trailingStop.other", "{0} shares, {1}% trail, stop ₡{2}" },
            { "order.card.detail", "{0} · {1}" },
            { "order.card.cancel", "Cancel" },
            { "portfolio.avgCost", "Avg cost / share:" },
            // City bonds (Economy → Loans)
            { "bond.title", "City bonds" },
            {
                "bond.about",
                "You can only have three loans at once. Bonds don't use a loan slot: the city pays a coupon every week and repays the face at maturity."
            },
            { "bond.sellTitle", "Sell a bond" },
            { "bond.term", "Term" },
            { "bond.weeks.one", "{0} Week" },
            { "bond.weeks.other", "{0} Weeks" },
            { "bond.amount", "Amount (₡)" },
            { "bond.rate", "Rate" },
            { "bond.coupon", "Weekly Cost" },
            { "bond.interest", "Total interest" },
            { "bond.total", "Total" },
            { "bond.repaidOn", "Repaid on" },
            { "bond.repaidOn.value", "{0} · {1}" },
            { "bond.sell", "Sell bond" },
            { "bond.sell.receive", "Sell bond · receive {0}" },
            { "bond.owedTitle", "Bonds you owe" },
            { "bond.owedTitle.count", "Bonds you owe ({0})" },
            { "bond.none", "None yet. Coupons and repayments show in the budget under loans." },
            { "bond.repay", "Pay Now" },
            {
                "bond.hint.off",
                "Selling new bonds is off (Options → City bonds). Bonds already sold keep paying."
            },
            {
                "bond.hint.amount",
                "Type a whole amount in ₡. Up to {0} before the city owes 3× its annual income."
            },
            { "bond.hint.room", "Up to {0} before the city owes 3× its annual income ({1})." },
            { "bond.refused.max", "Refused: one bond can raise at most {0}." },
            { "bond.refused", "Refused: {0}. Most you can raise: {1}." },
            { "bond.reason.debtCap", "the city would owe more than 3× its annual income" },
            { "bond.reason.noIncome", "the city has no income to borrow against" },
            { "bond.reason.badInput", "invalid bond request" },
            { "bond.rateLine", "{0} a year ({1} + {2})" },
            { "bond.card.defaultName", "Bond #{0}" },
            { "bond.card.nameTip", "Name this bond, for example after the project it pays for." },
            { "bond.card.amount", "Amount" },
            { "bond.card.term", "Term" },
            { "bond.card.interest", "Interest" },
            { "bond.card.total", "Total" },
            { "bond.card.weeklyCost", "Weekly Cost" },
            { "bond.card.timeLeft", "Time Left" },
            { "bond.card.repaidOn", "Repaid On" },
            // Local IPOs
            { "notice.tag.ipo", "#ipo" },
            {
                "notice.ipo.announced.one",
                "{0} is going public at {1} a share. The city can buy at the offering price for {2} day."
            },
            {
                "notice.ipo.announced.other",
                "{0} is going public at {1} a share. The city can buy at the offering price for {2} days."
            },
            {
                "notice.ipo.firstDay.up",
                "{0} opened at {1} on its first trading day, {2}% above the offering price."
            },
            {
                "notice.ipo.firstDay.down",
                "{0} opened at {1} on its first trading day, {2}% below the offering price."
            },
            {
                "notice.ipo.firstDay.flat",
                "{0} opened at {1} on its first trading day, level with the offering price."
            },
            {
                "notice.ipo.boughtOut.one",
                "{0} was bought out at {1} a share, {2}% above its price. The city received {3} for its {4} share."
            },
            {
                "notice.ipo.boughtOut.other",
                "{0} was bought out at {1} a share, {2}% above its price. The city received {3} for its {4} shares."
            },
            {
                "notice.ipo.boughtOut.none",
                "{0} was bought out at {1} a share, {2}% above its price. The city held no shares."
            },
            { "company.kind.Commercial", "Commercial" },
            { "company.kind.Office", "Office" },
            { "company.kind.Industry", "Industry" },
            { "company.kind.ItCluster", "IT cluster" },
            { "company.kind.Leisure", "Leisure" },
            { "company.kind.Tourism", "Tourism" },
            { "company.kind.Organic", "Organic and local produce" },
            { "company.kind.Farming", "Farming" },
            { "company.kind.Forestry", "Forestry" },
            { "company.kind.Oil", "Oil" },
            { "company.kind.Ore", "Ore" },
            { "ipo.card.section", "Local companies" },
            { "ipo.card.title.Commercial", "Commercial companies" },
            { "ipo.card.title.Office", "Office companies" },
            { "ipo.card.title.Industry", "Industrial companies" },
            { "ipo.card.title.ItCluster", "IT cluster companies" },
            { "ipo.card.title.Leisure", "Leisure companies" },
            { "ipo.card.title.Tourism", "Tourism companies" },
            { "ipo.card.title.Organic", "Organic and local produce companies" },
            { "ipo.card.title.Farming", "Farming companies" },
            { "ipo.card.title.Forestry", "Forestry companies" },
            { "ipo.card.title.Oil", "Oil companies" },
            { "ipo.card.title.Ore", "Ore companies" },
            {
                "ipo.card.locked",
                "Unlock requirement: {0} squares of these businesses in your city."
            },
            { "ipo.card.full", "{0} of {1} listed" },
            { "ipo.card.joined", "{0} · {1}" },
            { "ipo.card.slotsOpen.one", "{0} slot open" },
            { "ipo.card.slotsOpen.other", "{0} slots open" },
            { "ipo.card.candidates.one", "{0} candidate" },
            { "ipo.card.candidates.other", "{0} candidates" },
            { "ipo.card.note.open", "A listing is likely within one to two years." },
            { "ipo.card.note.waiting", "No business has been healthy long enough yet." },
            { "ipo.card.note.pacing", "Next listing possible from {0}." },
            { "ipo.card.note.boardFull", "The local board is full ({0} of {1})." },
            { "ipo.card.bar.unlock", "To unlock" },
            { "ipo.card.bar.nextSlot", "Next slot" },
            { "ipo.card.bar.value", "{0} / {1}" },
            {
                "ipo.card.footer",
                "Local listings {0} of {1} · {2} per map tile ({3} owned) · at most {4} IPOs a year"
            },
            { "ipo.dialog.title", "{0} is going public" },
            { "ipo.dialog.where.crossDistrict", "{0} · on {1} at {2}, {3}" },
            { "ipo.dialog.where.cross", "{0} · on {1} at {2}" },
            { "ipo.dialog.where.streetDistrict", "{0} · on {1}, {2}" },
            { "ipo.dialog.where.street", "{0} · on {1}" },
            { "ipo.dialog.where.district", "{0} · in {1}" },
            { "ipo.dialog.lead", "A business your city grew is listing on the Stock Exchange." },
            { "ipo.dialog.price", "Offering price" },
            { "ipo.dialog.price.value", "{0} a share" },
            { "ipo.dialog.window", "Offering window" },
            { "ipo.dialog.window.value.one", "{0} day · until {1}" },
            { "ipo.dialog.window.value.other", "{0} days · until {1}" },
            { "ipo.dialog.canBuy", "The city can buy" },
            { "ipo.dialog.canBuy.value", "up to {0} shares (a {1}% stake, {2})" },
            { "ipo.dialog.firstDay", "First trading day" },
            { "ipo.dialog.firstDay.value", "{0} · the price then moves freely" },
            {
                "ipo.dialog.smallPrint",
                "The headquarters is now a Historical building. Closing this changes nothing: the listing stays in Economy → Investments and the offering price holds for the window."
            },
            { "ipo.dialog.buy", "Buy at the offering price" },
            { "ipo.dialog.view", "View headquarters" },
            { "ipo.dialog.dontShow", "Don't show this for future IPOs" },
            { "ipo.dialog.closeTip", "Close (the listing stays; nothing else changes)" },
            { "options.ipo.dialog", "Show a dialog when a local company goes public" },
            {
                "company.description",
                "{0} is a company from your own city. It went public on the Stock Exchange, and the city can hold up to {1}% of it."
            },
            { "bond.owed", "Owed: loans {0} + bonds {1} = {2} · {3} annual income (cap {4}×)" },
            // Chirper notices
            { "notice.sender.brokerage", "City Brokerage" },
            { "notice.sender.treasury", "City Treasury" },
            { "notice.tag.orders", "#orders" },
            { "notice.tag.bonds", "#citybonds" },
            { "notice.tagged", "{0} {1}" },
            { "notice.title.limit", "Limit buy" },
            { "notice.title.stopLoss", "Stop-loss" },
            { "notice.title.takeProfit", "Take-profit" },
            { "notice.title.trailingStop", "Trailing stop" },
            {
                "notice.filled.limit",
                "Limit buy filled: bought {0} {1} at {2} (limit {3}) for {4}."
            },
            {
                "notice.filled.stopLoss",
                "Stop-loss filled: sold {0} {1} at {2} (stop {3}) for {4}."
            },
            {
                "notice.filled.takeProfit",
                "Take-profit filled: sold {0} {1} at {2} (target {3}) for {4}."
            },
            {
                "notice.filled.trailingStop",
                "Trailing stop filled: sold {0} {1} at {2} (stop {3}) for {4}."
            },
            { "notice.expired.one", "{0} for {1} {2} expired after {3} day without filling." },
            { "notice.expired.other", "{0} for {1} {2} expired after {3} days without filling." },
            { "notice.cancelled", "{0} for {1} {2} cancelled: {3}." },
            { "notice.why.funds", "not enough cash" },
            { "notice.why.fundsNeeds", "not enough cash (needs {0})" },
            { "notice.why.shareLimit", "the exchange has no more shares to sell" },
            { "notice.why.noShares", "the city no longer holds any {0}" },
            { "notice.why.cancelled", "cancelled" },
            { "notice.bond.matured", "Bond #{0} repaid at maturity: {1}." },
            {
                "notice.bond.maturedCoupon",
                "Bond #{0} repaid at maturity: {1} plus the last {2} coupon."
            },
            { "notice.bond.coupon", "Bond #{0} coupon paid: {1} ({2} of {3} left)." },
            {
                "notice.index.restored",
                "Your Index shares ({0}) are back. They were kept safe while Finance Overhaul was off."
            },
            { "notice.summary", "{0} order updates today: {1}. Details in Economy → Investments." },
            { "notice.summary.filled", "{0} filled ({1})" },
            { "notice.summary.expired", "{0} expired ({1})" },
            { "notice.summary.cancelled", "{0} cancelled ({1})" },
            // Welcome back dialog
            { "welcome.title", "Welcome back" },
            { "welcome.lead.one", "Finance Overhaul was off for {0} day in this city." },
            { "welcome.lead.other", "Finance Overhaul was off for {0} days in this city." },
            {
                "welcome.ask",
                "Your city bonds did not make their payments while it was off. How do you want to handle them?"
            },
            { "welcome.pay", "Pay what was missed ({0})" },
            {
                "welcome.payHelp.one",
                "Pays the {0} day of bond payments today, as if the mod had been on."
            },
            {
                "welcome.payHelp.other",
                "Pays the {0} days of bond payments today, as if the mod had been on."
            },
            { "welcome.skip", "Start fresh from today" },
            {
                "welcome.skipHelp",
                "Skips the missed payments. Payments carry on as normal from today."
            },
            { "welcome.footer", "This choice only affects this city." },
            { "welcome.close.tip", "Close: start fresh from today (the default)" },
            {
                "welcome.matured",
                "A bond reached its end while the mod was off: its {0} is repaid today either way."
            },
            { "welcome.indexBack", "Your Index shares ({0}) are back." },
            // Content Manager and Options page
            {
                "mod.description",
                "Stock Exchange overhaul for Financial Districts: limit, stop-loss, take-profit and trailing stop orders, a Stock Monitor, an Index stock, city bonds, a live LED ticker and Chirper notices."
            },
            { "options.ticker.live", "Live Stock Exchange ticker (visual only)" },
            { "options.ticker.shows", "Stock Exchange ticker shows" },
            { "options.ticker.shows.percent", "Change % (+3.22%)" },
            { "options.ticker.shows.price", "Price and ₡ change (₡951 +₡30)" },
            {
                "options.ticker.infoViews",
                "Show the LED ticker in info views (traffic, population…)"
            },
            { "options.ticker.brightness", "LED ticker brightness" },
            { "options.ticker.brightness.classic", "Classic" },
            { "options.ticker.brightness.bright", "Bright" },
            { "options.ticker.brightness.brightest", "Brightest (daylight)" },
            { "options.orders.lifetime", "Orders stay open for (new orders)" },
            { "options.notices", "Order and bond notices in Chirper" },
            { "options.notices.coupons", "Also chirp every bond coupon" },
            { "options.flags", "Feature flags (dev builds only)" },
            { "options.flag.limit", "Orders: Limit buys" },
            { "options.flag.stopLoss", "Orders: Stop-loss sells" },
            { "options.flag.takeProfit", "Orders: Take-profit sells" },
            { "options.flag.trailingStop", "Orders: Trailing stop sells" },
            { "options.flag.monitor", "Stock Monitor window" },
            { "options.flag.charts", "Share value charts in the Stock Monitor style" },
            { "options.flag.index", "Index stock (adds an investment to saves)" },
            { "options.flag.bonds", "City bonds (sell bonds to raise money; adds mod save data)" },
            {
                "options.flag.localIpos",
                "Local IPOs (being built: counts healthy businesses; adds mod save data)"
            },
            {
                "options.flag.testStock",
                "Dev: Test Stock (TST) for game UI tests (adds a listing; use a test save)"
            },
            { "options.copyBuildInfo", "Copy build info" },
            { "options.copiedBuildInfo", "Copied build info" },
        };
    }
}
