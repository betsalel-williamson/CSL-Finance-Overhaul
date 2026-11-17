using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Automated orders host (step 1: limit buy). The order book lives in the game's own
    /// mod data store (<see cref="OrderBookCodec.DataId"/>), written on every change: it
    /// goes into the save, survives DLL hot reload, and is ignored by the game when the
    /// mod is absent. Orders are checked once per in-game day, right after the vanilla
    /// daily investment update, and buy through vanilla <c>BuyShares</c>.
    /// </summary>
    public static class OrderDesk
    {
        private static readonly object Sync = new object();
        private static OrderBook book;
        private static int lastEvaluatedDay = -1;

        public static int Today
        {
            get { return OrderDay.DayOf(SimulationManager.instance.m_currentGameTime.Ticks); }
        }

        /// <summary>Places a limit buy for the investment with this id. Returns the order or null.</summary>
        public static TradeOrder PlaceLimitBuy(byte investmentId, uint shares, int limitPrice)
        {
            return Place(OrderKind.LimitBuy, investmentId, shares, limitPrice);
        }

        /// <summary>
        /// Places a sell order (stop-loss, take-profit or trailing stop) for the investment
        /// with this id. For a trailing stop <paramref name="triggerPrice"/> is the trail
        /// percent and today's price is the starting peak.
        /// Returns the order or null.
        /// </summary>
        public static TradeOrder PlaceSell(
            OrderKind kind,
            byte investmentId,
            uint shares,
            int triggerPrice
        )
        {
            return Place(kind, investmentId, shares, triggerPrice);
        }

        private static TradeOrder Place(OrderKind kind, byte investmentId, uint shares, int trigger)
        {
            try
            {
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                InvestmentInfo info = buffer[investmentId].Info;
                if (info == null)
                {
                    return null;
                }

                lock (Sync)
                {
                    OrderBook b = Book();
                    b.LifetimeDays = Mod.OrderLifetimeDays; // Options; open orders keep theirs
                    float mark = buffer[investmentId].value;
                    TradeOrder order = b.Place(kind, info.name, shares, trigger, mark, Today);
                    if (order != null)
                    {
                        Save(b);
                        ModLog.Info(OrderLog.Placed(order, mark));
                    }

                    return order;
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("orders: place failed: " + ex.Message);
                return null;
            }
        }

        public static bool Cancel(int id)
        {
            lock (Sync)
            {
                OrderBook b = Book();
                if (!b.Cancel(id))
                {
                    return false;
                }

                Save(b);
                ModLog.Info("orders: cancelled #" + id + " by the player");
                return true;
            }
        }

        /// <summary>Copy of the open orders for display.</summary>
        public static TradeOrder[] Snapshot()
        {
            lock (Sync)
            {
                var list = Book().Orders;
                var copy = new TradeOrder[list.Count];
                list.CopyTo(copy, 0);
                return copy;
            }
        }

        /// <summary>Postfix target: EconomyManager.SimulationStepInvestments (simulation thread).</summary>
        public static void OnInvestmentsStepped()
        {
            int today = Today;
            if (today == lastEvaluatedDay)
            {
                return;
            }

            lastEvaluatedDay = today;
            lock (Sync)
            {
                OrderBook b = Book();
                if (b.Orders.Count == 0)
                {
                    return;
                }

                bool changed = false;
                DayNotices.Clear();
                foreach (TradeOrder order in Snapshot(b))
                {
                    changed |= Evaluate(b, order, today);
                }

                ChirpNotices.PostDay(new List<Notice>(DayNotices));

                if (changed)
                {
                    Save(b);
                }
            }
        }

        /// <summary>Level load / unload: the next city reads its own book.</summary>
        public static void Reset()
        {
            lock (Sync)
            {
                book = null;
                lastEvaluatedDay = -1;
                Frozen.Clear();
            }
        }

        // Game UI tests: orders a running scenario did not create (the player's) are frozen —
        // no fill, no expiry, no trailing peak — even while the scenario advances days.
        private static readonly HashSet<int> Frozen = new HashSet<int>();

        // Today's order notices (fills, expiries, cancellations), posted together at the end.
        private static readonly List<Notice> DayNotices = new List<Notice>();

        /// <summary>Freezes these order ids until <see cref="UnfreezeAll"/> (game UI tests only).</summary>
        public static void Freeze(IEnumerable<int> ids)
        {
            lock (Sync)
            {
                foreach (int id in ids)
                {
                    Frozen.Add(id);
                }

                ModLog.Info(
                    "orders: " + Frozen.Count + " order(s) frozen while a game UI test runs"
                );
            }
        }

        public static void UnfreezeAll()
        {
            lock (Sync)
            {
                if (Frozen.Count > 0)
                {
                    ModLog.Info("orders: " + Frozen.Count + " frozen order(s) released");
                }

                Frozen.Clear();
            }
        }

        /// <summary>
        /// One order's day: reads its stock from the game, lets <see cref="OrderDay"/> decide,
        /// and carries the decision out. True when the book changed.
        /// </summary>
        private static bool Evaluate(OrderBook b, TradeOrder order, int today)
        {
            EconomyManager economy = EconomyManager.instance;
            Investment[] buffer = economy.m_investments.m_buffer;
            int id = Find(buffer, order.InvestmentKey);
            InvestmentAI ai = id < 0 ? null : buffer[id].Info.m_investmentAI;
            var listing = new ListingState { Listed = id >= 0, Symbol = order.InvestmentKey };
            if (ai != null)
            {
                listing.Mark = buffer[id].value;
                listing.Available = ai.AvailableForBuyingCount(ref buffer[id]);
                listing.Owned = ai.OwnedCount(ref buffer[id]);
                listing.Symbol = StockSymbols.For(buffer[id].Info);
            }

            // Whole ₡, the same unit as CalculatePrice (the vanilla dialog compares them directly).
            OrderStep step = OrderDay.Step(
                order,
                today,
                listing,
                EconomyManager.playerMoney,
                Mod.OrderKindEnabled(order.Kind),
                Frozen.Contains(order.Id)
            );
            switch (step.Kind)
            {
                case OrderStepKind.Sell:
                    int proceeds = ai.SellShares((byte)id, ref buffer[id], step.Shares);
                    // Same bookkeeping as the vanilla Sell button.
                    economy.m_lastInvestmentSell = SimulationManager.instance.m_currentGameTime;
                    step = OrderDay.Filled(order, step.Shares, listing, proceeds);
                    b.Cancel(order.Id);
                    if (ai.OwnedCount(ref buffer[id]) == 0)
                    {
                        RemovePortfolioRowOnMainThread((byte)id);
                    }

                    break;
                case OrderStepKind.Buy:
                    int cost = ai.BuyShares((byte)id, ref buffer[id], step.Shares);
                    // Same bookkeeping as the vanilla Buy button.
                    economy.m_lastInvestmentBuy = SimulationManager.instance.m_currentGameTime;
                    economy.m_tempSpendOnInvestment += cost;
                    step = OrderDay.Filled(order, step.Shares, listing, cost);
                    b.Cancel(order.Id);
                    AddPortfolioRowOnMainThread((byte)id);
                    break;
                case OrderStepKind.Remove:
                    b.Cancel(order.Id);
                    break;
            }

            if (step.Notice != null)
            {
                DayNotices.Add(step.Notice);
            }

            if (step.LogLine != null)
            {
                ModLog.Info(step.LogLine);
            }

            return step.BookChanged;
        }

        private static int Find(Investment[] buffer, string key)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                InvestmentAI ai = info == null ? null : info.m_investmentAI;
                if (ai != null && info.name == key && ai.IsAvailable((byte)i, ref buffer[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AddPortfolioRowOnMainThread(byte id)
        {
            try
            {
                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(() =>
                {
                    try
                    {
                        EconomyPanel panel = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                        if (panel != null)
                        {
                            panel.AddOwnedInvestment(
                                id,
                                ref EconomyManager.instance.m_investments.m_buffer[id]
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Warn("orders: portfolio refresh failed: " + ex.Message);
                    }
                });
            }
            catch
            {
                // fail soft: the portfolio list rebuilds when the panel reopens
            }
        }

        private static void RemovePortfolioRowOnMainThread(byte id)
        {
            try
            {
                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(() =>
                {
                    try
                    {
                        EconomyPanel panel = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                        if (panel != null)
                        {
                            panel.RemoveOwnedInvestment(id);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Warn("orders: portfolio refresh failed: " + ex.Message);
                    }
                });
            }
            catch
            {
                // fail soft: the portfolio list rebuilds when the panel reopens
            }
        }

        private static TradeOrder[] Snapshot(OrderBook b)
        {
            var copy = new TradeOrder[b.Orders.Count];
            b.Orders.CopyTo(copy, 0);
            return copy;
        }

        private static OrderBook Book()
        {
            if (book != null)
            {
                return book;
            }

            book = new OrderBook();
            try
            {
                byte[] data = SimulationManager.instance.m_SerializableDataWrapper.LoadData(
                    OrderBookCodec.DataId
                );
                if (data != null && data.Length > 0)
                {
                    book = OrderBookCodec.Parse(Encoding.UTF8.GetString(data));
                    ModLog.Info("orders: loaded " + book.Orders.Count + " open order(s)");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("orders: load failed, starting empty: " + ex.Message);
            }

            return book;
        }

        private static void Save(OrderBook b)
        {
            try
            {
                SimulationManager.instance.m_SerializableDataWrapper.SaveData(
                    OrderBookCodec.DataId,
                    Encoding.UTF8.GetBytes(OrderBookCodec.Format(b))
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("orders: save failed: " + ex.Message);
            }
        }
    }
}
