#if HAS_CITIES
using System;
using System.Collections.Generic;
using FinanceOverhaul.Market;

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

        public static string CurrentLine { get; private set; }

        /// <summary>Three rich-text board rows (ticker token colors), for the facade overlay.</summary>
        public static string[] CurrentRows { get; private set; }

        public const int BoardRows = 3;

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
            Symbols.Clear();
            Rates.Clear();
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

                Symbols.Add(TickerSymbols.For(ai.GetLocalizedName()));
                Rates.Add(ai.CalculateChangeRate((byte)i, ref buffer[i], PanelChangeRateWindow));
            }

            CurrentRows = TickerLine.FormatRichRows(Symbols, Rates, BoardRows);
            return TickerLine.Format(Symbols, Rates);
        }

        /// <summary>Hot reload: start fresh in the new assembly.</summary>
        public static void Reset()
        {
            lastDay = -1;
            daysLogged = 0;
            CurrentLine = null;
            CurrentRows = null;
        }
    }
}
#endif
