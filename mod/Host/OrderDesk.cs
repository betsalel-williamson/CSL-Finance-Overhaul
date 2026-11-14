#if HAS_CITIES
using System;
using System.Text;
using ColossalFramework;
using FinanceOverhaul.Market;
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
            get
            {
                return (int)(
                    SimulationManager.instance.m_currentGameTime.Ticks / TimeSpan.TicksPerDay
                );
            }
        }

        /// <summary>Places a limit buy for the investment with this id. Returns the order or null.</summary>
        public static TradeOrder PlaceLimitBuy(byte investmentId, uint shares, int limitPrice)
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
                    TradeOrder order = b.AddLimitBuy(info.name, shares, limitPrice, Today);
                    if (order != null)
                    {
                        Save(b);
                        ModLog.Info(
                            "orders: placed #"
                                + order.Id
                                + " limit buy "
                                + shares
                                + " "
                                + info.name
                                + " at or below "
                                + limitPrice
                                + " (expires day "
                                + order.ExpiresDay
                                + ")"
                        );
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
            try
            {
                InvestmentInfo info = EconomyManager
                    .instance
                    .m_investments
                    .m_buffer[investmentId]
                    .Info;
                if (info == null)
                {
                    return null;
                }

                lock (Sync)
                {
                    OrderBook b = Book();
                    b.LifetimeDays = Mod.OrderLifetimeDays; // Options; open orders keep theirs
                    float mark = EconomyManager.instance.m_investments.m_buffer[investmentId].value;
                    TradeOrder order;
                    switch (kind)
                    {
                        case OrderKind.TakeProfitSell:
                            order = b.AddTakeProfit(info.name, shares, triggerPrice, Today);
                            break;
                        case OrderKind.TrailingStopSell:
                            order = b.AddTrailingStop(info.name, shares, triggerPrice, mark, Today);
                            break;
                        default:
                            order = b.AddStopLoss(info.name, shares, triggerPrice, Today);
                            break;
                    }

                    if (order != null)
                    {
                        Save(b);
                        ModLog.Info(
                            "orders: placed #"
                                + order.Id
                                + " "
                                + OrderText.LogName(kind)
                                + " "
                                + shares
                                + " "
                                + info.name
                                + (
                                    kind == OrderKind.TrailingStopSell
                                        ? " trailing "
                                            + triggerPrice
                                            + "% from "
                                            + mark.ToString("0.##")
                                            + " (stop "
                                            + order.TriggerPrice
                                            + ")"
                                    : kind == OrderKind.TakeProfitSell
                                        ? " at or above " + triggerPrice
                                    : " at or below " + triggerPrice
                                )
                                + " (expires day "
                                + order.ExpiresDay
                                + ")"
                        );
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
                foreach (TradeOrder order in Snapshot(b))
                {
                    changed |= Evaluate(b, order, today);
                }

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
            }
        }

        private static bool Evaluate(OrderBook b, TradeOrder order, int today)
        {
            // Switched off in Options: kept as is (no fill, no expiry, no peak) until back on.
            if (!Mod.OrderKindEnabled(order.Kind))
            {
                return false;
            }

            EconomyManager economy = EconomyManager.instance;
            Investment[] buffer = economy.m_investments.m_buffer;
            int id = Find(buffer, order.InvestmentKey);
            float mark = 0f;
            uint available = 0;
            uint owned = 0;
            InvestmentAI ai = null;
            if (id >= 0)
            {
                ai = buffer[id].Info.m_investmentAI;
                mark = buffer[id].value;
                available = ai.AvailableForBuyingCount(ref buffer[id]);
                owned = ai.OwnedCount(ref buffer[id]);
            }

            // A locked or missing listing just waits (until it expires).
            if (id < 0 && today <= order.ExpiresDay)
            {
                return false;
            }

            // Whole ₡, the same unit as CalculatePrice (the vanilla dialog compares them directly).
            long cash = EconomyManager.playerMoney;
            OrderDecision d = OrderRules.Evaluate(order, today, mark, available, cash, owned);
            switch (d.Action)
            {
                case OrderAction.Fill when order.IsSell:
                    int proceeds = ai.SellShares((byte)id, ref buffer[id], d.Shares);
                    // Same bookkeeping as the vanilla Sell button.
                    economy.m_lastInvestmentSell = SimulationManager.instance.m_currentGameTime;
                    b.Cancel(order.Id);
                    ModLog.Info(
                        "orders: filled #"
                            + order.Id
                            + " sold "
                            + d.Shares
                            + " "
                            + order.InvestmentKey
                            + " at "
                            + mark.ToString("0.##")
                            + " ("
                            + OrderText.TriggerWord(order.Kind)
                            + " "
                            + order.TriggerPrice
                            + ") for "
                            + proceeds
                    );
                    if (ai.OwnedCount(ref buffer[id]) == 0)
                    {
                        RemovePortfolioRowOnMainThread((byte)id);
                    }

                    return true;
                case OrderAction.Fill:
                    int cost = ai.BuyShares((byte)id, ref buffer[id], d.Shares);
                    // Same bookkeeping as the vanilla Buy button.
                    economy.m_lastInvestmentBuy = SimulationManager.instance.m_currentGameTime;
                    economy.m_tempSpendOnInvestment += cost;
                    b.Cancel(order.Id);
                    ModLog.Info(
                        "orders: filled #"
                            + order.Id
                            + " bought "
                            + d.Shares
                            + " "
                            + order.InvestmentKey
                            + " at "
                            + mark.ToString("0.##")
                            + " (limit "
                            + order.TriggerPrice
                            + ") for "
                            + cost
                    );
                    AddPortfolioRowOnMainThread((byte)id);
                    return true;
                case OrderAction.Cancel:
                    b.Cancel(order.Id);
                    ModLog.Info(
                        "orders: cancelled #"
                            + order.Id
                            + " "
                            + OrderText.LogName(order.Kind)
                            + " "
                            + order.Shares
                            + " "
                            + order.InvestmentKey
                            + ": "
                            + d.Reason
                    );
                    return true;
                default:
                    if (d.PeakRaised)
                    {
                        ModLog.Info(
                            "orders: #"
                                + order.Id
                                + " trailing stop follows a new high "
                                + mark.ToString("0.##")
                                + " (stop "
                                + order.TriggerPrice
                                + ")"
                        );
                    }

                    return d.PeakRaised;
            }
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
#endif
