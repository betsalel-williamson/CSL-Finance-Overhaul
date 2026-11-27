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
    /// the frame (what the player saw, not a fresh render). With ffmpeg installed the raw
    /// pixels stream to it on a worker thread and become <c>NAME.mp4</c> (hardware H.264, no
    /// encoding in the game: <see cref="FfmpegSink"/>); without it, or after
    /// <c>record-format jpeg</c>, each frame is saved as a JPEG for
    /// <c>scripts/frames-to-video.sh</c>. Either way <c>frames.txt</c> keeps when each frame
    /// was shown. Unity 5.6 has no asynchronous GPU read, so the one read per frame stays. A glitch that lasts one frame (a line drawn
    /// one row late after a scroll) shows in the frames even when a still capture misses it.
    /// Optional crop to one UI control. Started by scenario steps (<c>record</c>,
    /// <c>record-stop</c>) or the <c>FinanceOverhaul.record</c> trigger file
    /// ("start NAME [CONTROL]" or "stop"). Stops by itself after <see cref="MaxSeconds"/>.
    /// Hot-reload safe: lives on the ticker root, and a stop waits for the writes.
    /// </summary>
    public partial class FrameRecorder : MonoBehaviour
    {
        public const string TriggerName = "FinanceOverhaul.record";
        public const float MaxSeconds = 30f;
        public const int Quality = 85;

        private static FrameRecorder instance;
        private static int pendingWrites;

        /// <summary>Save JPEGs even when ffmpeg is there (scenario step <c>record-format jpeg</c>).</summary>
        public static bool PreferJpeg;

        /// <summary>
        /// Count the frames without reading them (scenario step <c>record-format count</c>):
        /// the game's own frame rate over the same steps, to set a recording's cost against.
        /// </summary>
        public static bool CountOnly;

        /// <summary>
        /// Read a 16 × 16 patch each frame and keep nothing (<c>record-format probe</c>): what
        /// the read itself costs, apart from its size and from saving.
        /// </summary>
        public static bool ProbeOnly;
        private readonly System.Diagnostics.Stopwatch work = new System.Diagnostics.Stopwatch();
        private int workFrames;

        private FfmpegSink sink;
        private bool triedSink;
        private int dropped;
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
            instance.dropped = 0;
            instance.work.Reset();
            instance.workFrames = 0;
            instance.triedSink = false;
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

            string video = null;
            if (sink != null)
            {
                sink.Finish();
                dropped += sink.Dropped;
                video = sink.Output;
                sink = null;
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
                    + (dropped > 0 ? ", " + dropped + " dropped" : string.Empty)
                    + ", "
                    + (workFrames == 0 ? 0d : work.Elapsed.TotalMilliseconds / workFrames).ToString(
                        "0.00",
                        CultureInfo.InvariantCulture
                    )
                    + " ms a frame in the game"
                    + (video != null ? " -> " + video : " as JPEGs")
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
                    if (work.IsRunning)
                    {
                        work.Stop();
                        workFrames++;
                    }

                    yield return end;
                    work.Start();
                    if (Time.unscaledTime - startedAt > MaxSeconds)
                    {
                        ModLog.Info("record: " + MaxSeconds + " s reached");
                        StopNow();
                        yield break;
                    }

                    if (CountOnly)
                    {
                        frames++;
                        continue;
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

                    float now = Time.unscaledTime;
                    var area = ProbeOnly ? new Rect(r.x, r.y, 16f, 16f) : new Rect(r.x, r.y, w, h);
                    // Unity 5.6 can only read the screen synchronously: the CPU waits here for
                    // the GPU to finish the frame (about 20 ms in a big city, whatever the size
                    // read; a GPU copy read frames later waited just as long).
                    grab.ReadPixels(area, 0, 0, false);
                    if (ProbeOnly)
                    {
                        frames++;
                        continue;
                    }

                    if (!triedSink && !PreferJpeg)
                    {
                        triedSink = true;
                        sink = OpenSink(w, h);
                    }

                    if (sink != null)
                    {
                        // The video keeps the size it started with (a resized window is skipped).
                        if (
                            w == sink.Width
                            && h == sink.Height
                            && sink.Add(grab.GetRawTextureData())
                        )
                        {
                            frames++;
                            timing
                                .Append(frames)
                                .Append(' ')
                                .Append(
                                    (now - startedAt).ToString(
                                        "0.0000",
                                        CultureInfo.InvariantCulture
                                    )
                                )
                                .Append('\n');
                        }

                        continue;
                    }

                    byte[] jpg = grab.EncodeToJPG(Quality);
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

        /// <summary>The ffmpeg stream for a recording this size, or null (no ffmpeg, or it would not start).</summary>
        private FfmpegSink OpenSink(int w, int h)
        {
            string ffmpeg = FfmpegSink.Find();
            if (ffmpeg == null)
            {
                ModLog.Info("record: no ffmpeg found; saving JPEGs");
                return null;
            }

            try
            {
                int fps = Mathf.Clamp(
                    Mathf.RoundToInt(1f / Mathf.Max(0.001f, Time.smoothDeltaTime)),
                    5,
                    60
                );
                string output = Path.Combine(folder, Path.GetFileName(folder) + ".mp4");
                return new FfmpegSink(ffmpeg, output, w, h, fps);
            }
            catch (Exception ex)
            {
                ModLog.Warn("record: ffmpeg would not start (" + ex.Message + "); saving JPEGs");
                return null;
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
