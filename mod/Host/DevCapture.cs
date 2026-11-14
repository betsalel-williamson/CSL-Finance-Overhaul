#if HAS_CITIES
using System;
using System.IO;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Dev-only in-game screenshot for live testing (no OS screen-recording permission
    /// needed): when <c>FinanceOverhaul.capture</c> appears next to the settings file,
    /// saves the game's rendered frame to <c>FinanceOverhaul-capture.png</c> in the same
    /// folder and deletes the trigger. Active only in --dev builds
    /// (BuildInfo.ShowDevBuildIdentity). Hot-reload safe: lives on the ticker root.
    /// </summary>
    public class DevCapture : MonoBehaviour
    {
        public const string TriggerName = "FinanceOverhaul.capture";
        public const string OutputName = "FinanceOverhaul-capture.png";

        private float nextCheck;

        public static string Folder
        {
            get { return Path.GetDirectoryName(Mod.SettingsPath); }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 1f;
            try
            {
                string trigger = Path.Combine(Folder, TriggerName);
                if (!File.Exists(trigger))
                {
                    return;
                }

                File.Delete(trigger);
                string output = Path.Combine(Folder, OutputName);
                Application.CaptureScreenshot(output);
                ModLog.Info("dev capture: saved frame to " + output);
            }
            catch (Exception ex)
            {
                ModLog.Warn("dev capture failed: " + ex.Message);
            }
        }
    }
}
#endif
