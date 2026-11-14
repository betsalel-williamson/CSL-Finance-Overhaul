#if HAS_CITIES
using System;
using System.Collections.Generic;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Live ticker: scrolling LED boards (three dot-matrix rows fed from
    /// <see cref="TickerFeed.Current"/>) on the front facade of every Stock
    /// Exchange, in place of the hidden vanilla boards. Visual only, save-neutral,
    /// hot-reload safe (single root found by name and replaced; destroyed with its
    /// textures and materials on unload / disable).
    /// </summary>
    public static class TickerBoards
    {
        public const string RootName = "FinanceOverhaulTickerRoot";

        private static GameObject root;

        /// <summary>Main thread: after level load and after hot reload (PatchAll).</summary>
        public static void Ensure()
        {
            try
            {
                if (!LoadingManager.exists || !LoadingManager.instance.m_loadingComplete)
                {
                    return;
                }

                DestroyAll();
                root = new GameObject(RootName);
                root.AddComponent<TickerOverlay>();
                if (BuildInfo.ShowDevBuildIdentity)
                {
                    root.AddComponent<DevCapture>();
                }
                ModLog.Info("ticker: overlay attached");
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: overlay attach failed: " + ex.Message);
            }
        }

        /// <summary>Level unload, mod disable, hot reload. Also removes roots left by older assemblies.</summary>
        public static void DestroyAll()
        {
            try
            {
                VanillaBoards.RestoreAll();
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                    root = null;
                }

                GameObject stale;
                while ((stale = GameObject.Find(RootName)) != null)
                {
                    stale.name = RootName + " (destroying)";
                    UnityEngine.Object.Destroy(stale);
                }
            }
            catch
            {
                // fail soft
            }
        }
    }

    /// <summary>
    /// Per-frame driver on the overlay root (main thread). Each board screen shows three
    /// LED dot-matrix rows; each row is a ring-buffer marquee (<see cref="LedMarquee"/>)
    /// drawn into one repeat-wrapped texture shared by every Stock Exchange. The window
    /// only moves forward in whole dots and new entries are written just past its right
    /// edge, so values on screen never change and the scroll never jumps. Text clips
    /// exactly at the screen edge and the depth-tested quads hide behind anything in
    /// front of the facade.
    /// </summary>
    public class TickerOverlay : MonoBehaviour
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
        private static readonly Color32 BoardColour = new Color32(0x1E, 0x30, 0x3E, 0xFF);
        private static readonly Color32[] Palette =
        {
            new Color32(0x29, 0x3A, 0x46, 0xFF), // LedText.Off
            new Color32(0xEF, 0xB0, 0x58, 0xFF), // LedText.Symbol
            new Color32(0x4E, 0xF4, 0x65, 0xFF), // LedText.Gain
            new Color32(0xE9, 0x42, 0x4F, 0xFF), // LedText.Loss
            new Color32(0x9F, 0xB0, 0xC0, 0xFF), // LedText.Unknown
            new Color32(0x9F, 0xB0, 0xC0, 0xFF), // LedText.Flat (color/ticker/flat)
        };

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

        private void Update()
        {
            float now = Time.unscaledTime;
            try
            {
                bool tickerOn = Mod.Settings == null || Mod.Settings.LiveTickerEnabled;
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

        /// <summary>Finds Stock Exchanges (rare); placement is set once per building.</summary>
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

        private void EnsureMaterials()
        {
            if (materialsReady)
            {
                return;
            }

            materialsReady = true;
            panelMaterial = CreateUnlitMaterial(
                "FinanceOverhaulTickerPanel",
                SolidTexture(BoardColour)
            );
            frameMaterial = CreateFrameMaterial();
            for (int r = 0; r < RowCount; r++)
            {
                rowMaterials[r] = CreateUnlitMaterial("FinanceOverhaulTickerRow" + r, null);
                if (rowMaterials[r] != null)
                {
                    // Unlit/Transparent does not write depth, so the panel behind would paint
                    // over the rows in the same queue: draw the rows right after it.
                    rowMaterials[r].renderQueue = 2001;
                    CreateRow(r);
                    // Rows start full, so the boards never begin with an empty sweep.
                }
            }

            ModLog.Info(
                "ticker: LED boards "
                    + ScreenDots
                    + " dots wide, pitch "
                    + DotPitch.ToString("0.000")
                    + " m"
                    + (
                        rowMaterials[0] == null
                            ? " (Unlit/Transparent missing: no rows)"
                            : string.Empty
                    )
            );
        }

        /// <summary>
        /// Hands the newest day's entries to each row's marquee. Nothing is redrawn here:
        /// the marquees use the new values only for entries written from now on, beyond
        /// the right edge of the screen.
        /// </summary>
        private void RefreshContent()
        {
            TickerSnapshot snapshot = TickerFeed.Current;
            if (snapshot == null || ReferenceEquals(snapshot, appliedSnapshot))
            {
                return;
            }

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
            for (int r = 0; r < RowCount; r++)
            {
                LedMarquee marquee = marquees[r];
                if (marquee == null)
                {
                    continue;
                }

                long from;
                long to;
                marquee.Advance(steps, out from, out to);
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
        private void RestartAll()
        {
            for (int r = 0; r < RowCount; r++)
            {
                if (marquees[r] == null)
                {
                    continue;
                }

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

        /// <summary>One marquee row with its ring texture (readable, so columns can be repainted).</summary>
        private void CreateRow(int r)
        {
            var marquee = new LedMarquee(ScreenDots, r == 1 ? "Waiting for market data" : null);
            int w = marquee.RingDots * TexelsPerDot;
            int h = LedFont.Height * TexelsPerDot;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true);
            texture.name = "FinanceOverhaulTickerRing" + r;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            marquees[r] = marquee;
            rowTextures[r] = texture;
            rowPixels[r] = new Color32[w * h];
            Paint(r, 0, marquee.RingDots);
            texture.SetPixels32(rowPixels[r]);
            texture.Apply(true, false);
            rowMaterials[r].mainTexture = texture;
            // The window shows ScreenDots of the ring; the texture repeats across the seam.
            rowMaterials[r].mainTextureScale = new Vector2(
                ScreenDots / (float)marquee.RingDots,
                1f
            );
        }

        /// <summary>
        /// Repaints absolute columns [from, to) of row r's ring into its pixel buffer. Each
        /// dot is a lit block with a 1-texel dark gap on its right and bottom (LED look);
        /// dot row 0 is the top.
        /// </summary>
        private void Paint(int r, long from, long to)
        {
            LedStrip strip = marquees[r].Strip;
            Color32[] pixels = rowPixels[r];
            int t = TexelsPerDot;
            int w = strip.Width * t;
            for (long ax = from; ax < to; ax++)
            {
                int dx = (int)(ax % strip.Width);
                for (int dy = 0; dy < strip.Height; dy++)
                {
                    byte cell = strip.Get(dx, dy);
                    Color32 lit = cell < Palette.Length ? Palette[cell] : Palette[0];
                    for (int ty = 0; ty < t; ty++)
                    {
                        int texY = (strip.Height - 1 - dy) * t + (t - 1 - ty);
                        for (int tx = 0; tx < t; tx++)
                        {
                            bool gap = tx == t - 1 || ty == t - 1;
                            pixels[texY * w + dx * t + tx] = gap ? BoardColour : lit;
                        }
                    }
                }
            }
        }

        private static Texture2D SolidTexture(Color32 colour)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "FinanceOverhaulTickerSolid";
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Unlit/Transparent in the geometry queue: stays at full brightness day and night
        /// like the vanilla self-lit boards, and is depth-tested in Cities' render setup.
        /// </summary>
        private static Material CreateUnlitMaterial(string name, Texture texture)
        {
            Shader unlit = Shader.Find("Unlit/Transparent");
            if (unlit == null)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }

                return null;
            }

            var material = new Material(unlit);
            material.name = name;
            material.mainTexture = texture;
            material.renderQueue = 2000;
            return material;
        }

        /// <summary>Lit near-black frame so the board edges and depth catch the light.</summary>
        private static Material CreateFrameMaterial()
        {
            foreach (string name in new[] { "Legacy Shaders/Diffuse", "Diffuse" })
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    continue;
                }

                var material = new Material(shader);
                material.name = "FinanceOverhaulTickerFrame";
                material.color = new Color32(0x0E, 0x14, 0x18, 0xFF);
                return material;
            }

            return null;
        }

        /// <summary>
        /// One group per Stock Exchange. Children in order: Board0..2 (LED rows),
        /// Panel0..2 (dark screens), Frame0..2 (vanilla board meshes).
        /// </summary>
        private GameObject CreateBoard(ushort id)
        {
            var group = new GameObject("FO Ticker " + id);
            group.transform.SetParent(transform, false);
            for (int b = 0; b < BoardCentres.Length; b++)
            {
                var board = new GameObject("Board" + b);
                board.transform.SetParent(group.transform, false);
                for (int r = 0; r < RowCount; r++)
                {
                    if (rowMaterials[r] == null)
                    {
                        continue;
                    }

                    GameObject row = CreateQuad("Row" + r, board.transform, rowMaterials[r]);
                    row.transform.localScale = new Vector3(
                        ScreenDots * DotPitch,
                        LedFont.Height * DotPitch,
                        1f
                    );
                    row.transform.localPosition = new Vector3(
                        0f,
                        (1 - r) * (LedFont.Height + RowGapDots) * DotPitch,
                        0f
                    );
                }
            }

            for (int b = 0; b < BoardCentres.Length; b++)
            {
                GameObject panel = CreateQuad("Panel" + b, group.transform, panelMaterial);
                panel.transform.localScale = new Vector3(ScreenWidth, ScreenHeight, 1f);
            }

            return group;
        }

        private GameObject CreateQuad(string name, Transform parent, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            quad.transform.SetParent(parent, false);
            Renderer renderer = quad.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
                else
                {
                    renderer.enabled = false;
                }
            }

            return quad;
        }

        /// <summary>
        /// Real board geometry: redraws the vanilla board meshes (box frame, bezel and
        /// recess, already in the building's frame) with a plain lit near-black material
        /// instead of the baked scrolling texture.
        /// </summary>
        private void AddFrames(GameObject group, BuildingInfo info)
        {
            if (frameMaterial == null || info.m_subMeshes == null)
            {
                return;
            }

            if (group.transform.Find("Frame0") != null)
            {
                return;
            }

            int n = 0;
            for (int i = 0; i < info.m_subMeshes.Length; i++)
            {
                BuildingInfo.MeshInfo mesh = info.m_subMeshes[i];
                if (mesh == null || !VanillaBoards.IsBoard(mesh) || mesh.m_subInfo.m_mesh == null)
                {
                    continue;
                }

                var frame = new GameObject("Frame" + n++);
                frame.transform.SetParent(group.transform, false);
                // Undo the group's 180° turn so mesh vertices (building frame) line up.
                frame.transform.localPosition = Vector3.zero;
                frame.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                frame.AddComponent<MeshFilter>().sharedMesh = mesh.m_subInfo.m_mesh;
                frame.AddComponent<MeshRenderer>().sharedMaterial = frameMaterial;
            }
        }

        private void Place(GameObject group, ref Building b, BuildingInfo info, ushort id)
        {
            var front = new Vector3(-Mathf.Sin(b.m_angle), 0f, Mathf.Cos(b.m_angle));
            group.transform.position = b.m_position;
            // The group faces into the building, so a quad's visible (−Z) side faces the
            // street and reads left-to-right. That is a 180° turn about Y: building
            // (x, y, z) is group (−x, y, −z).
            group.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            for (int i = 0; i < BoardCentres.Length; i++)
            {
                Vector3 c = BoardCentres[i];
                group.transform.GetChild(i).localPosition = new Vector3(-c.x, c.y, -c.z);
                if (group.transform.childCount > BoardCentres.Length + i)
                {
                    group.transform.GetChild(BoardCentres.Length + i).localPosition = new Vector3(
                        -c.x,
                        c.y,
                        -(c.z - PanelBehindRows)
                    );
                }
            }

            AddFrames(group, info);

            if (!placementLogged)
            {
                placementLogged = true;
                ModLog.Info(
                    "ticker: boards for building "
                        + id
                        + " origin="
                        + b.m_position
                        + " angle="
                        + b.m_angle.ToString("0.###")
                );
            }
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

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.mainTexture != null)
            {
                Destroy(material.mainTexture);
            }

            Destroy(material);
        }
    }
}
#endif
