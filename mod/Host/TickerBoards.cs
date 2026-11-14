#if HAS_CITIES
using System;
using System.Collections.Generic;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Live ticker: scrolling LED boards (three dot-matrix rows from
    /// <see cref="TickerFeed.CurrentStrips"/>) on the front facade of every Stock
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
    /// LED dot-matrix rows. Once per in-game day each row is drawn into one wide texture
    /// (from <see cref="TickerFeed.CurrentStrips"/>); scrolling only moves that
    /// texture's offset in whole-dot steps, so text is clipped exactly at the screen edge
    /// and the depth-tested quads hide behind anything in front of the facade.
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
        internal const int MaxTextureWidth = 4096;
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
        };

        private readonly Dictionary<ushort, GameObject> boards =
            new Dictionary<ushort, GameObject>();
        private readonly Material[] rowMaterials = new Material[RowCount];
        private readonly int[] stripDots = new int[RowCount];
        private Material panelMaterial;
        private Material frameMaterial;
        private bool materialsReady;
        private float nextRefresh;
        private float nextLod;
        private float nextRescan;
        private LedStrip[] appliedStrips;
        private bool hasAppliedStrips;
        private bool anyNear;
        private float scrollTime;
        private int appliedStep = -1;
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
                        RefreshStrips();
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

        /// <summary>Texture work only when the daily strips change.</summary>
        private void RefreshStrips()
        {
            LedStrip[] strips = TickerFeed.CurrentStrips;
            if (hasAppliedStrips && ReferenceEquals(strips, appliedStrips))
            {
                return;
            }

            hasAppliedStrips = true;
            appliedStrips = strips;
            for (int r = 0; r < RowCount; r++)
            {
                Material material = rowMaterials[r];
                if (material == null)
                {
                    continue;
                }

                LedStrip strip =
                    strips == null
                        ? (
                            r == 1
                                ? LedText.RenderText("Waiting for market data", LedText.Unknown)
                                : null
                        )
                    : r < strips.Length ? strips[r]
                    : null;
                if (strip == null || strip.Width == 0)
                {
                    strip = new LedStrip(ScreenDots, LedFont.Height); // all dots off
                }

                Texture old = material.mainTexture;
                material.mainTexture = BuildTexture(strip);
                if (old != null)
                {
                    Destroy(old);
                }

                // BuildTexture keeps at most MaxTextureWidth dots (never reached in practice).
                stripDots[r] = Math.Min(strip.Width, MaxTextureWidth);
                material.mainTextureScale = new Vector2(ScreenDots / (float)stripDots[r], 1f);
            }

            appliedStep = -1; // re-apply offsets for the new widths
            Scroll(0f);
        }

        /// <summary>Moves each row left in whole-dot steps; the textures repeat seamlessly.</summary>
        private void Scroll(float deltaSeconds)
        {
            scrollTime += deltaSeconds;
            if (scrollTime > 3600f)
            {
                scrollTime -= 3600f; // keep float precision; one step jump per hour is invisible
            }

            int step = (int)(scrollTime * DotsPerSecond);
            if (step == appliedStep)
            {
                return;
            }

            appliedStep = step;
            for (int r = 0; r < RowCount; r++)
            {
                Material material = rowMaterials[r];
                int dots = stripDots[r];
                if (material == null || dots <= 0)
                {
                    continue;
                }

                material.mainTextureOffset = new Vector2((step % dots) / (float)dots, 0f);
            }
        }

        /// <summary>
        /// One strip → texture: each dot is a lit block with a 1-texel dark gap on its
        /// right and bottom; dot row 0 is the top. Repeat-wrapped for scrolling, mipmapped
        /// so distant boards blend instead of shimmering. CPU copy freed after upload.
        /// </summary>
        private static Texture2D BuildTexture(LedStrip strip)
        {
            int t = TexelsPerDot;
            while (t > 1 && strip.Width * t > MaxTextureWidth)
            {
                t--;
            }

            int dotsWide = Math.Min(strip.Width, MaxTextureWidth / t);
            int w = Math.Max(1, dotsWide * t);
            int h = strip.Height * t;
            var pixels = new Color32[w * h];
            for (int dy = 0; dy < strip.Height; dy++)
            {
                for (int dx = 0; dx < dotsWide; dx++)
                {
                    byte cell = strip.Get(dx, dy);
                    Color32 lit = cell < Palette.Length ? Palette[cell] : Palette[0];
                    for (int ty = 0; ty < t; ty++)
                    {
                        int texY = (strip.Height - 1 - dy) * t + (t - 1 - ty);
                        for (int tx = 0; tx < t; tx++)
                        {
                            bool gap = t > 1 && (tx == t - 1 || ty == t - 1);
                            pixels[texY * w + dx * t + tx] = gap ? BoardColour : lit;
                        }
                    }
                }
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true);
            texture.name = "FinanceOverhaulTickerStrip";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
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
