using System; // DateTime, Environment
using System.IO; // File, Path

namespace FinanceOverhaul
{
    public static class ModLog
    {
        private static readonly object Gate = new object();
        private static string _path;

        public static string LogPath
        {
            get
            {
                if (_path != null)
                {
                    return _path;
                }

                string tmp = Environment.GetEnvironmentVariable("TMPDIR");
                if (string.IsNullOrEmpty(tmp))
                {
                    tmp = Path.GetTempPath();
                }

                _path = Path.Combine(tmp, "finance-overhaul.log");
                return _path;
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        private static void Write(string level, string message)
        {
            string line =
                DateTime.UtcNow.ToString("o") + " [" + level + "] " + (message ?? string.Empty);
            lock (Gate)
            {
                try
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
                catch
                {
                    // fail soft
                }
            }

            try
            {
                UnityEngine.Debug.Log("[FinanceOverhaul] " + message);
            }
            catch
            {
                // fail soft
            }
        }
    }
}
