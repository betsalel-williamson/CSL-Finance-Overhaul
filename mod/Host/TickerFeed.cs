using System;
using System.Collections.Generic;
using FinanceOverhaul.Core;

namespace FinanceOverhaul
{
    /// <summary>
    /// Live Stock Exchange ticker data (step 1 of the live ticker): once per in-game
    /// day, builds <c>SYM ( +2.67 )</c> entries for every unlocked investment from the
    /// same vanilla calls the Economy panel uses — GetLocalizedName and
    /// CalculateChangeRate(id, ref data, 5), rounded to two decimals — so the ticker
    /// always matches the panel. Read-only and save-neutral. Rendering on the building
    /// is a later step; for now the line is logged and kept in <see cref="CurrentLine"/>.
    /// </summary>
    public static class TickerFeed
    {
        /// <summary>Window the Economy panel passes to CalculateChangeRate.</summary>
        public const int PanelChangeRateWindow = 5;

        private static int lastDay = -1;
        private static int daysLogged;
        private static bool failureLogged;

        private static readonly List<string> Symbols = new List<string>(20);
        private static readonly List<float> Rates = new List<float>(20);
        private static readonly Dictionary<string, float> PriceBySymbol = new Dictionary<
            string,
            float
        >(20);

        public static string CurrentLine { get; private set; }

        /// <summary>Today's sorted entries for the facade boards (one object, swapped atomically).</summary>
        public static TickerSnapshot Current { get; private set; }

        public const int BoardRows = 3;

        private static readonly object BuildLock = new object();

        /// <summary>
        /// Builds today's entries now if there are none yet (level load, hot reload), so the
        /// boards start full instead of "Waiting for market data" until the next game day.
        /// Main thread; the same read-only calls as the daily build.
        /// </summary>
        public static void Prime()
        {
            if (Current != null)
            {
                return;
            }

            try
            {
                string line = Build();
                if (Current != null && Current.Count > 0)
                {
                    CurrentLine = line;
                    ModLog.Info("ticker: primed " + Current.Count + " entries at start");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: prime failed: " + ex.Message);
            }
        }

        /// <summary>Postfix target: EconomyManager.SimulationStepInvestments (simulation thread).</summary>
        public static void OnInvestmentsStepped()
        {
            try
            {
                int day = SimulationManager.instance.m_currentGameTime.Day;
                if (day == lastDay)
                {
                    return;
                }

                lastDay = day;
                CurrentLine = Build();
                daysLogged++;
                if (daysLogged <= 10 || daysLogged % 10 == 0)
                {
                    ModLog.Info("ticker: " + CurrentLine);
                }
            }
            catch (Exception ex)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    ModLog.Warn("ticker: build failed: " + ex.Message);
                }
            }
        }

        private static string Build()
        {
            lock (BuildLock)
            {
                return BuildLocked();
            }
        }

        private static string BuildLocked()
        {
            Symbols.Clear();
            Rates.Clear();
            PriceBySymbol.Clear();
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                InvestmentAI ai = info == null ? null : info.m_investmentAI;
                if (ai == null || !ai.IsAvailable((byte)i, ref buffer[i]))
                {
                    continue;
                }

                string symbol = StockSymbols.For(info);
                Symbols.Add(symbol);
                PriceBySymbol[symbol] = buffer[i].value; // the game's "Value", whole ₡
                Rates.Add(ai.CalculateChangeRate((byte)i, ref buffer[i], PanelChangeRateWindow));
            }

            TickerLine.SortBySymbol(Symbols, Rates);
            var prices = new float[Symbols.Count];
            for (int k = 0; k < prices.Length; k++)
            {
                float p;
                prices[k] = PriceBySymbol.TryGetValue(Symbols[k], out p) ? p : float.NaN;
            }

            Current = new TickerSnapshot(Symbols.ToArray(), Rates.ToArray(), prices);
            return TickerLine.Format(Symbols, Rates);
        }

        /// <summary>Hot reload: start fresh in the new assembly.</summary>
        public static void Reset()
        {
            lastDay = -1;
            daysLogged = 0;
            CurrentLine = null;
            Current = null;
        }
    }
}
