using System; // Exception
using System.Collections.Generic; // List, Queue
using FinanceOverhaul.Core; // FeedLog, Notice, NoticeBatch
using ICities; // IChirperMessage
using UnityEngine; // MonoBehaviour

// Game classes used without a using (global namespace, Assembly-CSharp): ChirpPanel

namespace FinanceOverhaul
{
    /// <summary>
    /// Order and bond notices in the game's Chirper (priority 1, Figma band 13). Daily steps on
    /// the simulation thread hand over each day's notices (collapsed by <see cref="NoticeBatch"/>);
    /// <see cref="ChirpNoticePoster"/> posts them on the main thread through
    /// <c>ChirpPanel.AddMessage</c> with a plain <see cref="IChirperMessage"/>: the panel only, never
    /// the game's MessageManager, so nothing reaches the save. Muted while a game UI test runs.
    /// </summary>
    public static class ChirpNotices
    {
        private static readonly object Sync = new object();
        private static readonly Queue<Notice> Outbox = new Queue<Notice>();

        /// <summary>Set while a game UI test runs (tests never chirp in the player's city).</summary>
        public static bool Muted;

        /// <summary>This session's notices for the Investments overview's Feed (kept even when muted or off).</summary>
        public static readonly FeedLog Log = new FeedLog();

        public static bool Enabled
        {
            get { return Mod.Settings == null || Mod.Settings.NoticesEnabled; }
        }

        /// <summary>One game day's notices from one source (orders or bonds).</summary>
        public static void PostDay(List<Notice> day)
        {
            if (day == null || day.Count == 0)
            {
                return;
            }

            int today = OrderDesk.Today;
            foreach (Notice n in day)
            {
                Log.Add(today, n);
            }

            List<Notice> collapsed = NoticeBatch.Collapse(day);
            lock (Sync)
            {
                foreach (Notice n in collapsed)
                {
                    if (Muted || !Enabled)
                    {
                        ModLog.Info(
                            "notices: not posted (" + (Muted ? "muted" : "off") + "): " + n.Text
                        );
                        continue;
                    }

                    Outbox.Enqueue(n);
                }
            }
        }

        /// <summary>Main thread: posts what is waiting.</summary>
        internal static void Pump()
        {
            while (true)
            {
                Notice n;
                lock (Sync)
                {
                    if (Outbox.Count == 0)
                    {
                        return;
                    }

                    n = Outbox.Dequeue();
                }

                try
                {
                    ChirpPanel panel = ChirpPanel.instance;
                    if (panel == null)
                    {
                        ModLog.Warn("notices: no Chirper panel; dropped: " + n.Text);
                        continue;
                    }

                    panel.AddMessage(new FinanceChirp(n.Sender, n.Text), true);
                    ModLog.Info("notices: chirped [" + n.Sender + "] " + n.Text);
                }
                catch (Exception ex)
                {
                    ModLog.Warn("notices: chirp failed: " + ex.Message);
                }
            }
        }

        public static void Reset()
        {
            lock (Sync)
            {
                Outbox.Clear();
            }

            Log.Clear();
        }

        /// <summary>A chirp that lives only in the Chirper panel.</summary>
        private sealed class FinanceChirp : IChirperMessage
        {
            private readonly string sender;
            private readonly string message;

            public FinanceChirp(string sender, string message)
            {
                this.sender = sender;
                this.message = message;
            }

            public string senderName
            {
                get { return sender; }
            }

            public string text
            {
                get { return message; }
            }

            public uint senderID
            {
                get { return 0; }
            }
        }
    }

    /// <summary>Posts waiting notices on the main thread (lives on the overlay root).</summary>
    public class ChirpNoticePoster : MonoBehaviour
    {
        private void Update()
        {
            ChirpNotices.Pump();
        }
    }
}
