using System; // Exception, StringSplitOptions
using System.Collections; // IEnumerator
using System.Globalization; // CultureInfo
using System.IO; // Directory, File, Path
using System.Text; // StringBuilder
using System.Threading; // Interlocked, Thread, ThreadPool
using ColossalFramework.UI; // UIComponent, UIView
// UnityEngine: Coroutine, Mathf, MonoBehaviour, Rect, Screen, Texture2D, TextureFormat, Time,
//   Vector2, Vector3, WaitForEndOfFrame
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Dev-only video capture: every frame the game shows, read from the screen at the end of
    /// the frame (what the player saw, not a fresh render), saved as numbered JPEGs with the
    /// time each was shown, so <c>scripts/frames-to-video.sh</c> can make an MP4 at the real
    /// pace and a contact sheet to look through. A glitch that lasts one frame (a line drawn
    /// one row late after a scroll) shows in the frames even when a still capture misses it.
    /// Optional crop to one UI control. Started by scenario steps (<c>record</c>,
    /// <c>record-stop</c>) or the <c>FinanceOverhaul.record</c> trigger file
    /// ("start NAME [CONTROL]" or "stop"). Stops by itself after <see cref="MaxSeconds"/>.
    /// Hot-reload safe: lives on the ticker root, and a stop waits for the writes.
    /// </summary>
    public class FrameRecorder : MonoBehaviour
    {
        public const string TriggerName = "FinanceOverhaul.record";
        public const float MaxSeconds = 30f;
        public const int Quality = 85;

        private static FrameRecorder instance;
        private static int pendingWrites;

        private Coroutine loop;
        private bool stopping;
        private string folder;
        private int frames;
        private float startedAt;
        private readonly StringBuilder timing = new StringBuilder();
        private float nextCheck;

        /// <summary>The recording's folder while one runs (null when none).</summary>
        public static string Recording
        {
            get { return instance != null && instance.loop != null ? instance.folder : null; }
        }

        /// <summary>Frames saved by the last (or running) recording.</summary>
        public static int Frames
        {
            get { return instance == null ? 0 : instance.frames; }
        }

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                StopNow();
                instance = null;
            }
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
                string trigger = Path.Combine(DevCapture.Folder, TriggerName);
                if (!File.Exists(trigger))
                {
                    return;
                }

                string text = File.ReadAllText(trigger).Trim();
                File.Delete(trigger);
                string[] w = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (w.Length >= 2 && w[0] == "start")
                {
                    Start(w[1], w.Length > 2 ? w[2] : null);
                }
                else if (w.Length >= 1 && w[0] == "stop")
                {
                    StopNow();
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("record: trigger failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Starts recording into <c>FinanceOverhaul-video-NAME/</c> next to the settings, the
        /// whole screen or only <paramref name="control"/>'s rectangle. Null when started, else
        /// why not.
        /// </summary>
        public static string Start(string name, string control)
        {
            if (instance == null)
            {
                return "no recorder (not a --dev build)";
            }

            instance.StopNow();
            UIComponent crop = null;
            if (!string.IsNullOrEmpty(control))
            {
                crop = UiTestRunner.FindShown(control);
                if (crop == null)
                {
                    return control + " is not on screen";
                }
            }

            string folder = Path.Combine(DevCapture.Folder, "FinanceOverhaul-video-" + name);
            if (Directory.Exists(folder))
            {
                foreach (string old in Directory.GetFiles(folder))
                {
                    File.Delete(old);
                }
            }
            else
            {
                Directory.CreateDirectory(folder);
            }

            instance.folder = folder;
            instance.frames = 0;
            instance.stopping = false;
            instance.timing.Length = 0;
            instance.startedAt = Time.unscaledTime;
            instance.loop = instance.StartCoroutine(instance.Record(crop));
            ModLog.Info(
                "record: started "
                    + folder
                    + (crop == null ? " (whole screen)" : " (" + control + ")")
                    + "; the game was at "
                    + (1f / Mathf.Max(0.001f, Time.smoothDeltaTime)).ToString(
                        "0",
                        CultureInfo.InvariantCulture
                    )
                    + " fps"
            );
            return null;
        }

        /// <summary>Stops the recording (if any) and waits for its frames to be written.</summary>
        public static void Stop()
        {
            if (instance != null)
            {
                instance.StopNow();
            }
        }

        private void StopNow()
        {
            if (loop == null)
            {
                return;
            }

            stopping = true;
            StopCoroutine(loop);
            loop = null;
            // The frames are written on worker threads: wait (briefly) for the last ones.
            float until = Time.realtimeSinceStartup + 5f;
            while (Interlocked.CompareExchange(ref pendingWrites, 0, 0) > 0)
            {
                if (Time.realtimeSinceStartup > until)
                {
                    ModLog.Warn("record: frames still being written after 5 s");
                    break;
                }

                Thread.Sleep(10);
            }

            try
            {
                File.WriteAllText(Path.Combine(folder, "frames.txt"), timing.ToString());
            }
            catch (Exception ex)
            {
                ModLog.Warn("record: frames.txt: " + ex.Message);
            }

            ModLog.Info(
                "record: stopped "
                    + folder
                    + ", "
                    + frames
                    + " frames in "
                    + (Time.unscaledTime - startedAt).ToString("0.0", CultureInfo.InvariantCulture)
                    + " s"
            );
        }

        private IEnumerator Record(UIComponent crop)
        {
            var end = new WaitForEndOfFrame();
            Texture2D grab = null;
            float last = Time.unscaledTime;
            try
            {
                while (!stopping)
                {
                    yield return end;
                    if (Time.unscaledTime - startedAt > MaxSeconds)
                    {
                        ModLog.Info("record: " + MaxSeconds + " s reached");
                        StopNow();
                        yield break;
                    }

                    Rect r = ScreenRect(crop);
                    int w = Mathf.Max(2, (int)r.width) & ~1; // even sizes: video encoders need them
                    int h = Mathf.Max(2, (int)r.height) & ~1;
                    if (grab == null || grab.width != w || grab.height != h)
                    {
                        if (grab != null)
                        {
                            Destroy(grab);
                        }

                        grab = new Texture2D(w, h, TextureFormat.RGB24, false);
                    }

                    grab.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0, false);
                    byte[] jpg = grab.EncodeToJPG(Quality);
                    float now = Time.unscaledTime;
                    frames++;
                    string file = Path.Combine(
                        folder,
                        "frame-" + frames.ToString("00000", CultureInfo.InvariantCulture) + ".jpg"
                    );
                    if (frames > 1)
                    {
                        // The previous frame stayed on screen until this one.
                        timing
                            .Append("duration ")
                            .Append((now - last).ToString("0.0000", CultureInfo.InvariantCulture))
                            .Append('\n');
                    }

                    timing.Append("file '").Append(Path.GetFileName(file)).Append("'\n");
                    last = now;
                    Interlocked.Increment(ref pendingWrites);
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            File.WriteAllBytes(file, jpg);
                        }
                        catch (Exception ex)
                        {
                            ModLog.Warn("record: " + ex.Message);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref pendingWrites);
                        }
                    });
                }
            }
            finally
            {
                if (grab != null)
                {
                    Destroy(grab);
                }
            }
        }

        /// <summary>The control's rectangle in screen pixels (origin bottom left), or the screen.</summary>
        private static Rect ScreenRect(UIComponent crop)
        {
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            if (crop == null || !crop.isVisible)
            {
                return screen;
            }

            Vector2 ui = UIView.GetAView().GetScreenResolution();
            float k = Screen.height / Mathf.Max(1f, ui.y);
            Vector3 p = crop.absolutePosition;
            float x = Mathf.Clamp(p.x * k, 0f, Screen.width - 2f);
            float top = Mathf.Clamp(p.y * k, 0f, Screen.height - 2f);
            float w = Mathf.Min(crop.width * k, Screen.width - x);
            float h = Mathf.Min(crop.height * k, Screen.height - top);
            return new Rect(Mathf.Floor(x), Mathf.Floor(Screen.height - top - h), w, h);
        }
    }
}
