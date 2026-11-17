using System;
using System.Collections.Generic;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>Index listing: the daily price (mark) from the basket of sector stocks, and its one-time rebase.</summary>
    public static partial class IndexListing
    {
        /// <summary>
        /// Postfix target: InvestmentAI.SafeCalculateValue (after vanilla random
        /// variance, so Index carries none). Index level is chain-linked:
        /// previous level × (1 + mean latest return of unlocked share stocks).
        /// Seeds from the price average when Index has no history yet.
        /// </summary>
        public static void ApplyMark(InvestmentAI ai, ref Investment data, ref float value)
        {
            if (!IsIndex(ai))
            {
                return;
            }

            try
            {
                ReadBasket();
                RebaseOnce(ai, ref data);
                float previous =
                    data.allValues != null && data.allValues.Count > 0 ? data.value : 0f;
                float mark;
                string mode;
                if (
                    CityIndexBasket.TryComputeChainedMark(
                        previous,
                        Names,
                        Last,
                        Prev,
                        Eligible,
                        out mark
                    )
                )
                {
                    mode = "chain";
                }
                else if (
                    !(previous > 0f)
                    && CityIndexBasket.TryComputeMark(Names, Last, Eligible, out mark)
                )
                {
                    mode = "seed";
                }
                else if (previous > 0f)
                {
                    mark = previous;
                    mode = "hold";
                }
                else
                {
                    if (!markFailureLogged)
                    {
                        markFailureLogged = true;
                        ModLog.Warn(
                            "index: no unlocked share prices readable; keeping vanilla mark"
                        );
                    }

                    return;
                }

                value = mark;
                LogUpdate(mode, previous, mark);
            }
            catch (Exception ex)
            {
                if (!markFailureLogged)
                {
                    markFailureLogged = true;
                    ModLog.Warn("index: mark failed, keeping vanilla mark: " + ex.Message);
                }
            }
        }

        /// <summary>Level load / unload: the next city reads its own marker.</summary>
        public static void ResetRebase()
        {
            rebaseDone = null;
            lastRebaseWaitDay = -1;
        }

        /// <summary>
        /// One-time correction (see <see cref="IndexRebase"/>): scales an Index inflated by the
        /// old mean-of-returns rule, and its whole history, to the members' average price.
        /// Simulation thread, inside the Index's daily update, before today's step. Needs the
        /// basket already read (<see cref="ReadBasket"/>).
        /// </summary>
        private static void RebaseOnce(InvestmentAI ai, ref Investment data)
        {
            try
            {
                if (rebaseDone == null)
                {
                    byte[] marker = SimulationManager.instance.m_SerializableDataWrapper.LoadData(
                        IndexRebase.DataId
                    );
                    rebaseDone = marker != null && marker.Length > 0;
                }

                if (rebaseDone.Value || data.allValues == null || data.allValues.Count == 0)
                {
                    return;
                }

                float average;
                if (!CityIndexBasket.TryComputeMark(Names, Last, Eligible, out average))
                {
                    return; // no members readable yet; try again tomorrow
                }

                int openOrders = 0;
                foreach (TradeOrder o in OrderDesk.Snapshot())
                {
                    if (CityIndexBasket.IsIndexName(o.InvestmentKey))
                    {
                        openOrders++;
                    }
                }

                IndexRebase.Plan plan = IndexRebase.Decide(
                    data.value,
                    average,
                    ai.OwnedCount(ref data),
                    openOrders
                );
                if (plan.Decision == IndexRebase.Decision.Wait)
                {
                    if (OrderDesk.Today != lastRebaseWaitDay)
                    {
                        lastRebaseWaitDay = OrderDesk.Today;
                        ModLog.Info("index: rebase waiting: " + plan.Reason);
                    }

                    return;
                }

                float before = data.value;
                if (plan.Decision == IndexRebase.Decision.Rescale)
                {
                    Investment.Queue<float> q = data.allValues;
                    var oldestFirst = new float[q.Count];
                    for (int j = 0; j < oldestFirst.Length; j++)
                    {
                        oldestFirst[j] = q.GetFromTop(oldestFirst.Length - 1 - j);
                    }

                    float[] scaled = IndexRebase.Scale(oldestFirst, plan.Factor);
                    data.ClearValues();
                    foreach (float v in scaled)
                    {
                        data.AddValue(v);
                    }
                }

                SimulationManager.instance.m_SerializableDataWrapper.SaveData(
                    IndexRebase.DataId,
                    System.Text.Encoding.UTF8.GetBytes(IndexRebase.Marker)
                );
                rebaseDone = true;
                ModLog.Info(
                    "index: rebase "
                        + (plan.Decision == IndexRebase.Decision.Rescale ? "applied" : "not needed")
                        + " ("
                        + plan.Reason
                        + "): "
                        + before.ToString("0.##")
                        + " -> "
                        + data.value.ToString("0.##")
                        + " (members' average "
                        + average.ToString("0.##")
                        + ")"
                );
            }
            catch (Exception ex)
            {
                rebaseDone = true; // never retry a failing correction every day
                ModLog.Warn("index: rebase failed, level kept: " + ex.Message);
            }
        }

        /// <summary>First 20 updates after (re)load, then every 30th, to keep the log small.</summary>
        private static void LogUpdate(string mode, float previous, float mark)
        {
            updateCount++;
            if (updateCount > 20 && updateCount % 30 != 0)
            {
                return;
            }

            float change = previous > 0f ? (mark / previous - 1f) * 100f : 0f;
            ModLog.Info(
                "index: update #"
                    + updateCount
                    + " mode="
                    + mode
                    + " "
                    + previous.ToString("0.##")
                    + " -> "
                    + mark.ToString("0.##")
                    + " ("
                    + change.ToString("+0.00;-0.00")
                    + "%) members="
                    + CountEligible()
            );
        }

        /// <summary>One-line readout for the log after a level loads.</summary>
        public static void LogStatus()
        {
            try
            {
                if (indexInfo == null)
                {
                    ModLog.Info("index: not registered (Financial Districts missing?)");
                    return;
                }

                bool listed = false;
                float current = float.NaN;
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                    {
                        continue;
                    }

                    InvestmentInfo info = buffer[i].Info;
                    if (info != null && CityIndexBasket.IsIndexName(info.m_name))
                    {
                        listed = true;
                        current = buffer[i].value;
                        break;
                    }
                }

                TryReadBasketMark(out float basket);
                ModLog.Info(
                    "index: listed="
                        + listed
                        + " value="
                        + current
                        + " basket="
                        + basket
                        + " sectors="
                        + CountEligible()
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: status failed: " + ex.Message);
            }
        }

        private static void ReadBasket()
        {
            Names.Clear();
            Last.Clear();
            Prev.Clear();
            Eligible.Clear();

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                if (info == null || info.m_investmentAI == null)
                {
                    continue;
                }

                Investment.Queue<float> history = buffer[i].allValues;
                int count = history == null ? 0 : history.Count;
                Names.Add(info.m_name);
                // GetFromTop(0) is the newest value, GetFromTop(1) the one before it.
                Last.Add(count > 0 ? history.GetFromTop(0) : float.NaN);
                Prev.Add(count > 1 ? history.GetFromTop(1) : float.NaN);
                Eligible.Add(
                    IsSectorStock(info) && info.m_investmentAI.IsAvailable((byte)i, ref buffer[i])
                );
            }
        }

        private static bool TryReadBasketMark(out float mark)
        {
            ReadBasket();
            return CityIndexBasket.TryComputeMark(Names, Last, Eligible, out mark);
        }

        private static int CountEligible()
        {
            int n = 0;
            for (int i = 0; i < Eligible.Count; i++)
            {
                if (Eligible[i] && !CityIndexBasket.IsIndexName(Names[i]))
                {
                    n++;
                }
            }

            return n;
        }
    }
}
