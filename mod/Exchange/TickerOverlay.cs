using System; // Exception, Math
using System.Collections.Generic; // Dictionary, HashSet, KeyValuePair, List
// FinanceOverhaul.Core: LedBrightness, LedFont, LedMarquee, LedText, TickerDisplay, TickerSnapshot
using FinanceOverhaul.Core;
// UnityEngine: Camera, Color32, GameObject, Material, Mathf, MonoBehaviour, Quaternion, Texture2D,
//   Time, Vector2, Vector3
using UnityEngine;

// Game classes used without a using (global namespace, Assembly-CSharp): Building, BuildingInfo,
//   BuildingManager, InfoManager, StockExchangeAI

namespace FinanceOverhaul
{
    /// <summary>
    /// Per-frame driver on the overlay root (main thread). Each board screen shows three
    /// LED dot-matrix rows; each row is a ring-buffer marquee (<see cref="LedMarquee"/>)
    /// drawn into one repeat-wrapped texture shared by every Stock Exchange. The window
    /// only moves forward in whole dots and new entries are written just past its right
    /// edge, so values on screen never change and the scroll never jumps. Text clips
    /// exactly at the screen edge and the depth-tested quads hide behind anything in
    /// front of the facade.
    /// </summary>
    public partial class TickerOverlay : MonoBehaviour
    {
        // Exact vanilla board rectangles, read from the Stock Exchange's three AnimUV
        // sub-meshes (Submesh01..03) in the building's own frame (+Z = street side):
        // centre x −7.7..7.7, y 15.2..18.6; sides x ±(5.5..20.8), y 10.8..14.1; face z 23.6.
        // Screen recessed inside the vanilla bezel (bezel lip runs z 23.6 → 23.4).
        internal static readonly Vector3[] BoardCentres =
        {
            new Vector3(0f, 16.9f, 23.5f), // centre (Submesh01)
            new Vector3(-13.15f, 12.45f, 23.5f), // left (Submesh02)
            new Vector3(13.15f, 12.45f, 23.5f), // right (Submesh03)
        };
        internal const float BoardWidth = 15.3f;
        internal const float BoardHeight = 3.4f;

        // The vanilla frame has a 0.3 m bezel; our screen fills that opening.
        internal const float Bezel = 0.3f;
        internal const float ScreenWidth = BoardWidth - 2f * Bezel; // 14.7 m
        internal const float ScreenHeight = BoardHeight - 2f * Bezel; // 2.8 m

        // Dark panel 2 cm behind the LED rows: no z-fighting, no visible parallax.
        internal const float PanelBehindRows = 0.02f;

        // LED layout: three rows of 8-dot glyphs, 2 dark dot rows between them, padded
        // inside the screen. Dots are square.
        internal const float PaddingX = 0.15f;
        internal const float PaddingY = 0.24f;
        internal const int RowGapDots = 2;
        internal const int RowCount = TickerFeed.BoardRows;
        internal static readonly float DotPitch =
            (ScreenHeight - 2f * PaddingY)
            / (RowCount * LedFont.Height + (RowCount - 1) * RowGapDots); // ≈ 0.083 m
        internal static readonly int ScreenDots = Mathf.FloorToInt(
            (ScreenWidth - 2f * PaddingX) / DotPitch
        ); // ≈ 173

        // Each dot is TexelsPerDot² texels: lit block plus a 1-texel dark gap (LED look).
        internal const int TexelsPerDot = 4;
        internal const float DotsPerSecond = 14f;

        // Performance tiers: LED rows draw only within VisibleDistance; textures are
        // rebuilt only when the daily strips change; the building scan is rare.
        internal const float VisibleDistance = 600f;
        internal const float LodSeconds = 0.25f;
        internal const float RefreshSeconds = 1f;
        internal const float RescanSeconds = 5f;

        // Figma tokens color/ticker/*: board, symbol, gain, loss, unknown; plus a dim
        // unlit-dot shade between board and board-lit.
        private static readonly Color32 BoardColour = FoTheme.LedBoard;

        // Dot colours for the chosen Options brightness (LedPalette, Market); rebuilt on change.
        private Color32[] palette;
        private Color32[] gaps;
        private LedBrightness paletteLevel = (LedBrightness)(-1);

        private readonly Dictionary<ushort, GameObject> boards =
            new Dictionary<ushort, GameObject>();
        private readonly Material[] rowMaterials = new Material[RowCount];
        private readonly LedMarquee[] marquees = new LedMarquee[RowCount];
        private readonly Texture2D[] rowTextures = new Texture2D[RowCount];
        private readonly Color32[][] rowPixels = new Color32[RowCount][];
        private Material panelMaterial;
        private Material frameMaterial;
        private bool materialsReady;
        private float nextRefresh;
        private float nextLod;
        private float nextRescan;
        private TickerSnapshot appliedSnapshot;
        private bool anyNear;
        private float scrollDots;
        private bool placementLogged;
        private bool lastShown = true;
        private TickerDisplay appliedDisplay = (TickerDisplay)(-1);

        /// <summary>The style the boards' current screens were drawn in (game UI tests read it).</summary>
        internal static TickerDisplay? DrawnStyle { get; private set; }

        /// <summary>Whether the live boards are drawn now (game UI tests read it).</summary>
        internal static bool ShownNow { get; private set; } = true;

        private void Update()
        {
            float now = Time.unscaledTime;
            try
            {
                bool infoView =
                    InfoManager.exists
                    && InfoManager.instance.CurrentMode != InfoManager.InfoMode.None;
                bool tickerOn = Mod.Settings == null || Mod.Settings.TickerShownNow(infoView);
                // Info views switch in OnInfoModeChanged; this catches Options changes.
                ApplyShown(tickerOn);

                if (tickerOn && !FollowBuildings())
                {
                    nextRescan = now; // one was removed or replaced: look for its successor now
                }

                if (now >= nextRescan)
                {
                    nextRescan = now + RescanSeconds;
                    if (tickerOn)
                    {
                        Rescan();
                    }
                    else
                    {
                        VanillaBoards.RestoreAll();
                    }
                }

                if (now >= nextLod)
                {
                    nextLod = now + LodSeconds;
                    UpdateVisibility(tickerOn);
                }

                if (now >= nextRefresh)
                {
                    nextRefresh = now + RefreshSeconds;
                    if (tickerOn)
                    {
                        RefreshContent();
                    }
                }

                if (anyNear)
                {
                    Scroll(Time.unscaledDeltaTime);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: overlay update failed: " + ex.Message);
                enabled = false; // stop this component; vanilla boards remain
            }
        }

        /// <summary>
        /// LOD: panels and frames stay while the ticker is on (the vanilla boards are
        /// hidden); the LED rows draw only when the camera is close enough to read them.
        /// </summary>
        private void UpdateVisibility(bool tickerOn)
        {
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
            float maxSqr = VisibleDistance * VisibleDistance;
            bool nearAny = false;
            foreach (GameObject group in boards.Values)
            {
                if (group == null)
                {
                    continue;
                }

                if (group.activeSelf != tickerOn)
                {
                    group.SetActive(tickerOn);
                }

                Vector3 toEye = eye - group.transform.GetChild(0).position;
                bool near = tickerOn && camera != null && toEye.sqrMagnitude <= maxSqr;
                nearAny |= near;
                for (int i = 0; i < BoardCentres.Length && i < group.transform.childCount; i++)
                {
                    GameObject board = group.transform.GetChild(i).gameObject;
                    if (board.activeSelf != near)
                    {
                        board.SetActive(near);
                    }
                }
            }

            if (nearAny && !anyNear)
            {
                // Rows were hidden: safe to refill them with today's values before they show.
                RestartAll();
            }

            anyNear = nearAny;
        }

        /// <summary>
        /// Null when every set of boards stands where its Stock Exchange stands now, else
        /// which one does not (game UI tests read it).
        /// </summary>
        internal string Misplaced()
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            foreach (KeyValuePair<ushort, GameObject> kv in boards)
            {
                if (kv.Value == null)
                {
                    continue;
                }

                Vector3 at = kv.Value.transform.position;
                float facing = Quaternion.Angle(
                    kv.Value.transform.rotation,
                    Facing(buffer[kv.Key].m_angle)
                );
                if ((at - buffer[kv.Key].m_position).sqrMagnitude > 0.0001f || facing > 0.1f)
                {
                    return "boards of building " // not shown: read by game UI tests
                        + kv.Key
                        + " are "
                        + (at - buffer[kv.Key].m_position).magnitude.ToString("0.##")
                        + " m and "
                        + facing.ToString("0.#")
                        + "° from it"; // not shown: read by game UI tests
                }
            }

            return null;
        }

        /// <summary>
        /// Every frame: boards stay on their Stock Exchange when it is moved or turned (the
        /// relocate tool and Move It keep the building's number). A handful of comparisons,
        /// since a city has few exchanges. False when a building is gone or is no longer a
        /// Stock Exchange, so the caller scans at once instead of waiting.
        /// </summary>
        private bool FollowBuildings()
        {
            bool allThere = true;
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            foreach (KeyValuePair<ushort, GameObject> kv in boards)
            {
                GameObject group = kv.Value;
                if (group == null)
                {
                    continue;
                }

                BuildingInfo info = buffer[kv.Key].Info;
                if (
                    (buffer[kv.Key].m_flags & Building.Flags.Created) == 0
                    || info == null
                    || !(info.m_buildingAI is StockExchangeAI)
                )
                {
                    allThere = false;
                    continue;
                }

                if (
                    group.transform.position != buffer[kv.Key].m_position
                    || group.transform.rotation != Facing(buffer[kv.Key].m_angle)
                )
                {
                    Place(group, ref buffer[kv.Key], info, kv.Key);
                }
            }

            return allThere;
        }

        /// <summary>Finds Stock Exchanges (rare): new ones get boards, removed ones lose them.</summary>
        private void Rescan()
        {
            EnsureMaterials();
            var seen = new HashSet<ushort>();
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            for (int i = 1; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Building.Flags.Created) == 0)
                {
                    continue;
                }

                BuildingInfo info = buffer[i].Info;
                if (info == null || !(info.m_buildingAI is StockExchangeAI))
                {
                    continue;
                }

                ushort id = (ushort)i;
                seen.Add(id);
                VanillaBoards.Hide(info); // idempotent; shared per asset
                GameObject group;
                if (!boards.TryGetValue(id, out group) || group == null)
                {
                    group = CreateBoard(id);
                    boards[id] = group;
                    Place(group, ref buffer[i], info, id);
                }
            }

            var gone = new List<ushort>();
            foreach (KeyValuePair<ushort, GameObject> kv in boards)
            {
                if (!seen.Contains(kv.Key))
                {
                    gone.Add(kv.Key);
                }
            }

            foreach (ushort id in gone)
            {
                Destroy(boards[id]);
                boards.Remove(id);
            }
        }

        /// <summary>
        /// Hands the newest day's entries to each row's marquee. Nothing is redrawn here:
        /// the marquees use the new values only for entries written from now on, beyond
        /// the right edge of the screen.
        /// </summary>
        private void RefreshContent()
        {
            TickerFeed.Prime(); // first time: build today's entries instead of waiting a day
            TickerSnapshot snapshot = TickerFeed.Current;
            if (snapshot == null || ReferenceEquals(snapshot, appliedSnapshot))
            {
                return;
            }

            bool first = appliedSnapshot == null;
            appliedSnapshot = snapshot;
            for (int r = 0; r < RowCount; r++)
            {
                if (marquees[r] == null)
                {
                    continue;
                }

                int start;
                int count;
                LedText.RowRange(snapshot.Count, RowCount, r, out start, out count);
                marquees[r].SetContent(snapshot, start, count);
            }

            if (first)
            {
                // The rows were written with the waiting text: fill the whole screen with data
                // now (nothing has been on show long enough to notice a jump).
                RestartAll();
            }
        }

        /// <summary>
        /// Traditional scroller: every row's window moves left by whole dots (never
        /// backwards, never reset), and each marquee writes its next entry into the ring
        /// just beyond the right edge. Only those freshly written columns are repainted.
        /// </summary>
        private void Scroll(float deltaSeconds)
        {
            scrollDots += deltaSeconds * DotsPerSecond;
            int steps = (int)scrollDots;
            if (steps <= 0)
            {
                return;
            }

            scrollDots -= steps;
            TickerDisplay display = DisplayNow();
            if (display != appliedDisplay)
            {
                // The style changed: redraw every row's whole screen in it, never a mix.
                appliedDisplay = display;
                RestartAll();
            }

            bool recolour = UpdatePalette();
            for (int r = 0; r < RowCount; r++)
            {
                LedMarquee marquee = marquees[r];
                if (marquee == null)
                {
                    continue;
                }

                // Options → "Stock Exchange ticker shows": entries written from now on use it.
                marquee.Display = DisplayNow();
                long from;
                long to;
                marquee.Advance(steps, out from, out to);
                if (recolour)
                {
                    // New brightness: repaint the whole ring (same dots, new colours).
                    from = Math.Max(0L, marquee.Head - marquee.RingDots);
                    to = from + marquee.RingDots;
                }

                if (to > from)
                {
                    Paint(r, from, to);
                    rowTextures[r].SetPixels32(rowPixels[r]);
                    rowTextures[r].Apply(true, false);
                }

                rowMaterials[r].mainTextureOffset = new Vector2(
                    marquee.RingOffset / (float)marquee.RingDots,
                    0f
                );
            }
        }

        /// <summary>
        /// Only while no board is visible: refill each window from the current values so
        /// the rows do not come back showing a stale day.
        /// </summary>
        private static TickerDisplay DisplayNow()
        {
            return Mod.Settings != null ? Mod.Settings.TickerShows : TickerDisplay.ChangePercent;
        }

        private void RestartAll()
        {
            for (int r = 0; r < RowCount; r++)
            {
                if (marquees[r] == null)
                {
                    continue;
                }

                marquees[r].Display = DisplayNow(); // every entry on the new screen in one style
                DrawnStyle = marquees[r].Display;
                marquees[r].Restart();
                Paint(r, 0, marquees[r].RingDots);
                rowTextures[r].SetPixels32(rowPixels[r]);
                rowTextures[r].Apply(true, false);
                rowMaterials[r].mainTextureOffset = new Vector2(
                    marquees[r].RingOffset / (float)marquees[r].RingDots,
                    0f
                );
            }
        }

        private void OnEnable()
        {
            if (InfoManager.exists)
            {
                InfoManager.instance.EventInfoModeChanged += OnInfoModeChanged;
            }
        }

        private void OnDisable()
        {
            if (InfoManager.exists)
            {
                InfoManager.instance.EventInfoModeChanged -= OnInfoModeChanged;
            }
        }

        /// <summary>
        /// The game raises this when it actually switches the info view, before that frame is
        /// drawn. Swapping between the live boards and the vanilla ones right here (instead of
        /// in the next Update) means the vanilla boards never flash their baked scroll.
        /// </summary>
        private void OnInfoModeChanged(InfoManager.InfoMode mode, InfoManager.SubInfoMode subMode)
        {
            try
            {
                bool shown =
                    Mod.Settings == null
                    || Mod.Settings.TickerShownNow(mode != InfoManager.InfoMode.None);
                ApplyShown(shown);
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: info view switch failed: " + ex.Message);
            }
        }

        /// <summary>Shows or hides the live boards now, hiding or restoring the vanilla ones with them.</summary>
        private void ApplyShown(bool shown)
        {
            if (shown == lastShown)
            {
                return;
            }

            lastShown = shown;
            ShownNow = shown;
            if (shown)
            {
                Rescan(); // hides the vanilla boards again
            }
            else
            {
                VanillaBoards.RestoreAll();
            }

            UpdateVisibility(shown);
            float now = Time.unscaledTime;
            nextRescan = now + RescanSeconds;
            nextLod = now + LodSeconds;
        }

        private void OnDestroy()
        {
            foreach (GameObject b in boards.Values)
            {
                if (b != null)
                {
                    Destroy(b);
                }
            }

            boards.Clear();

            // Hot reload / unload: free everything this overlay created.
            for (int r = 0; r < RowCount; r++)
            {
                DestroyMaterial(rowMaterials[r]);
                rowMaterials[r] = null;
            }

            DestroyMaterial(panelMaterial);
            DestroyMaterial(frameMaterial);
            panelMaterial = null;
            frameMaterial = null;
        }
    }
}
