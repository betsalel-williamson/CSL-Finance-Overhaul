using System; // Exception
using System.Collections.Generic; // Queue
using System.Diagnostics; // Process, ProcessStartInfo
using System.Globalization; // CultureInfo
using System.IO; // File, Stream
using System.Threading; // Monitor, Thread

namespace FinanceOverhaul
{
    public partial class FrameRecorder
    {
        /// <summary>
        /// Streams raw frames into one ffmpeg process that encodes H.264 (the Mac's
        /// VideoToolbox hardware encoder; libx264 elsewhere). The main thread only copies the
        /// screen's pixels; a worker thread writes them to ffmpeg's input, so the game loses
        /// no time to image encoding. Every shown frame goes into the video once, at the game's
        /// frame rate when the recording started; <c>frames.txt</c> keeps when each was shown.
        /// A frame that arrives while <see cref="Backlog"/> frames still wait is dropped (and
        /// counted), so a slow disk never stalls the game.
        /// </summary>
        private sealed class FfmpegSink
        {
            public const int Backlog = 90;

            private static readonly string[] Places =
            {
                "/opt/homebrew/bin/ffmpeg", // not shown: file paths
                "/usr/local/bin/ffmpeg", // not shown: file paths
                "/usr/bin/ffmpeg", // not shown: file paths
            };

            private readonly Process process;
            private readonly Stream input;
            private readonly Thread writer;
            private readonly Queue<byte[]> queue = new Queue<byte[]>();
            private bool closed;

            public int Width { get; private set; }
            public int Height { get; private set; }
            public int Dropped { get; private set; }
            public string Output { get; private set; }

            /// <summary>The ffmpeg the recorder can run, or null.</summary>
            public static string Find()
            {
                foreach (string place in Places)
                {
                    if (File.Exists(place))
                    {
                        return place;
                    }
                }

                return null;
            }

            public FfmpegSink(string ffmpeg, string output, int width, int height, int fps)
            {
                Width = width;
                Height = height;
                Output = output;
                bool mac =
                    UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXPlayer;
                string encoder = mac
                    ? "-c:v h264_videotoolbox -b:v 16M" // not shown: ffmpeg arguments
                    : "-c:v libx264 -preset ultrafast -crf 18"; // not shown: ffmpeg arguments
                string args = string.Format(
                    CultureInfo.InvariantCulture,
                    "-loglevel error -y -f rawvideo -pix_fmt rgb24 -s {0}x{1} -framerate {2} -i - " // not shown: ffmpeg arguments
                        + "-vf vflip {3} -pix_fmt yuv420p \"{4}\"", // not shown: ffmpeg arguments
                    width,
                    height,
                    fps,
                    encoder,
                    output
                );
                process = new Process
                {
                    StartInfo = new ProcessStartInfo(ffmpeg, args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                    },
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ModLog.Warn("record: ffmpeg: " + e.Data);
                    }
                };
                process.Start();
                process.BeginErrorReadLine();
                input = process.StandardInput.BaseStream;
                writer = new Thread(Write) { IsBackground = true, Name = "FO frame writer" };
                writer.Start();
            }

            /// <summary>Queues a frame (main thread); false when the backlog is full and it was dropped.</summary>
            public bool Add(byte[] rgb)
            {
                lock (queue)
                {
                    if (closed)
                    {
                        return false;
                    }

                    if (queue.Count >= Backlog)
                    {
                        Dropped++;
                        return false;
                    }

                    queue.Enqueue(rgb);
                    Monitor.Pulse(queue);
                    return true;
                }
            }

            private void Write()
            {
                try
                {
                    while (true)
                    {
                        byte[] frame;
                        lock (queue)
                        {
                            while (queue.Count == 0 && !closed)
                            {
                                Monitor.Wait(queue);
                            }

                            if (queue.Count == 0)
                            {
                                return;
                            }

                            frame = queue.Dequeue();
                        }

                        input.Write(frame, 0, frame.Length);
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warn("record: writing to ffmpeg failed: " + ex.Message);
                }
            }

            /// <summary>Writes what is queued, ends the video and waits for ffmpeg (up to 20 s).</summary>
            public void Finish()
            {
                lock (queue)
                {
                    closed = true;
                    Monitor.PulseAll(queue);
                }

                writer.Join(20000);
                try
                {
                    input.Close();
                    if (!process.WaitForExit(20000))
                    {
                        ModLog.Warn("record: ffmpeg did not finish in 20 s; stopped");
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warn("record: ending the video failed: " + ex.Message);
                }
                finally
                {
                    process.Close();
                }
            }
        }
    }
}
