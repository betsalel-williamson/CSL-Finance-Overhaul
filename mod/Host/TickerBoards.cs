#if HAS_CITIES
using System;
using System.Collections.Generic;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Live ticker step 2a: draws one live board (three rich-text rows from
    /// <see cref="TickerFeed.CurrentRows"/>) on the front facade of every Stock
    /// Exchange. Visual only, save-neutral, hot-reload safe (single root found by
    /// name and replaced; destroyed on unload / disable). Placement uses vanilla
    /// Building.CalculatePosition; local +Z is the lot front (sidewalk side).
    /// Offsets are first guesses, logged for calibration.
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

    /// <summary>Per-frame driver on the overlay root (main thread).</summary>
    public class TickerOverlay : MonoBehaviour
    {
        // Exact vanilla board rectangles, read from the Stock Exchange's three AnimUV
        // sub-meshes (Submesh01..03) in the building's own frame (+Z = street side):
        // centre x −7.7..7.7, y 15.2..18.6; sides x ±(5.5..20.8), y 10.8..14.1; face z 23.6.
        internal static readonly Vector3[] BoardCentres =
        {
            // Text and panel share one plane just in front of the old board face (z 23.6), so
            // there is no parallax between them at steep camera angles. The text ignores
            // depth (GUI/Text Shader), so being coplanar with the panel cannot hide it.
            new Vector3(0f, 16.9f, 23.65f), // centre (Submesh01)
            new Vector3(-13.15f, 12.45f, 23.65f), // left (Submesh02)
            new Vector3(13.15f, 12.45f, 23.65f), // right (Submesh03)
        };
        internal const float BoardWidth = 15.3f;
        internal const float PanelBehindText = 0f; // coplanar: no parallax
        internal const float BoardHeight = 3.4f;

        // Padding from the Figma FO/TickerBoard design (600 px board: 14 px sides ≈ 2.5%,
        // 8 px top/bottom ≈ 7%), scaled to the 15.3 × 3.4 m vanilla board.
        internal const float PaddingX = 0.4f;
        internal const float PaddingY = 0.24f;
        internal const float MaxRowWidth = BoardWidth - 2f * PaddingX; // 14.5 m
        internal const float RowSpacing = (BoardHeight - 2f * PaddingY) / 3f; // ≈ 0.97 m
        internal const float RowHeight = 0.8f; // metres per row of text
        internal const float InnerHeight = BoardHeight - 2f * PaddingY; // 2.92 m
        internal const int FontSize = 64;

        // Performance tiers: visibility is a distance check per Stock Exchange; text work
        // happens only when the daily rows change; the building scan is rare.
        internal const float VisibleDistance = 600f; // metres; beyond this the text is unreadable
        internal const float LodSeconds = 0.25f;
        internal const float RefreshSeconds = 1f;
        internal const float RescanSeconds = 5f;

        private static readonly string[] SerifFonts =
        {
            "Goudy Old Style",
            "Hoefler Text",
            "Baskerville",
            "Georgia",
            // Unity's font enumeration on macOS lists Palatino and Times but not the others.
            "Palatino",
            "Times New Roman",
            "Times",
        };

        private readonly Dictionary<ushort, GameObject> boards =
            new Dictionary<ushort, GameObject>();
        private Font font;
        private Material textMaterial;
        private Material panelMaterial;
        private float nextRefresh;
        private float nextLod;
        private float nextRescan;
        private string[] appliedRows;
        private bool hasAppliedRows;
        private int fitPasses;
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
                        RefreshText();
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: overlay update failed: " + ex.Message);
                enabled = false; // stop this component; vanilla boards remain
            }
        }

        /// <summary>
        /// LOD: a board group is active only when the camera is close enough to read it.
        /// Inactive groups cost nothing to draw or update; off-screen ones are also
        /// frustum-culled by Unity.
        /// </summary>
        private void UpdateVisibility(bool tickerOn)
        {
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
            float maxSqr = VisibleDistance * VisibleDistance;
            foreach (GameObject group in boards.Values)
            {
                if (group == null)
                {
                    continue;
                }

                // Panels stay while the ticker is on (the vanilla boards are hidden, so the
                // facade still needs boards); only the text is distance-culled.
                if (group.activeSelf != tickerOn)
                {
                    group.SetActive(tickerOn);
                }

                // Text draws over geometry (GUI/Text Shader), so show it only when the camera
                // is in front of the facade (group forward points into the building) and
                // close enough to read it.
                Vector3 toEye = eye - group.transform.GetChild(0).position;
                bool near =
                    tickerOn
                    && camera != null
                    && toEye.sqrMagnitude <= maxSqr
                    && Vector3.Dot(toEye, group.transform.forward) < 0f;
                for (int i = 0; i < BoardCentres.Length && i < group.transform.childCount; i++)
                {
                    GameObject board = group.transform.GetChild(i).gameObject;
                    if (board.activeSelf != near)
                    {
                        board.SetActive(near);
                        if (near)
                        {
                            fitPasses = Math.Max(fitPasses, 2);
                        }
                    }
                }
            }
        }

        /// <summary>Finds Stock Exchanges (rare); placement is set once per building.</summary>
        private void Rescan()
        {
            if (font == null)
            {
                font = PickFont();
                textMaterial = CreateDepthTestedMaterial(font);
                panelMaterial = CreatePanelMaterial();
            }

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
                    SetRows(group, appliedRows);
                    fitPasses = Math.Max(fitPasses, 2);
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

        /// <summary>Text work only when the daily rows change (plus two fit passes).</summary>
        private void RefreshText()
        {
            if (textMaterial != null && font != null && font.material != null)
            {
                // Dynamic fonts rebuild their atlas when new glyphs are requested.
                textMaterial.mainTexture = font.material.mainTexture;
            }

            string[] rows = TickerFeed.CurrentRows;
            if (!hasAppliedRows || !ReferenceEquals(rows, appliedRows))
            {
                hasAppliedRows = true;
                appliedRows = rows;
                foreach (GameObject group in boards.Values)
                {
                    if (group != null)
                    {
                        SetRows(group, rows);
                    }
                }

                fitPasses = 2; // the text mesh rebuilds lazily, so fit now and once more
            }

            if (fitPasses > 0)
            {
                fitPasses--;
                foreach (GameObject group in boards.Values)
                {
                    if (group != null && group.activeSelf)
                    {
                        Fit(group);
                    }
                }
            }
        }

        /// <summary>
        /// CreateDynamicFontFromOSFont names the font after the first requested name even
        /// when it is missing (and silently falls back to a sans), so pick from the fonts
        /// that are actually installed and log the real choice.
        /// </summary>
        private static Font PickFont()
        {
            string[] installed = Font.GetOSInstalledFontNames() ?? new string[0];
            // Installed names can carry style suffixes ("Baskerville Regular"), so match by
            // prefix in preference order.
            for (int i = 0; i < SerifFonts.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    string name = installed[j];
                    if (
                        name != null
                        && name.StartsWith(SerifFonts[i], StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        Font f = Font.CreateDynamicFontFromOSFont(name, FontSize);
                        if (f != null)
                        {
                            ModLog.Info("ticker: board font=" + name + " (installed)");
                            return f;
                        }
                    }
                }
            }

            var serifLike = new List<string>();
            for (int j = 0; j < installed.Length && serifLike.Count < 15; j++)
            {
                string n = installed[j] ?? string.Empty;
                if (
                    n.IndexOf("Goudy", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Baskerville", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Georgia", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Times", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Hoefler", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Garamond", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Palatino", StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    serifLike.Add(n);
                }
            }

            ModLog.Warn(
                "ticker: no preferred serif matched ("
                    + installed.Length
                    + " fonts; serif-like: "
                    + string.Join(", ", serifLike.ToArray())
                    + "; first: "
                    + string.Join(", ", installed, 0, Math.Min(8, installed.Length))
                    + "); using Arial"
            );
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static readonly string[] ProbeShaders =
        {
            "Legacy Shaders/Diffuse",
            "Diffuse",
            "Standard",
            "Unlit/Color",
            "Unlit/Texture",
            "Unlit/Transparent",
            "Sprites/Default",
            "UI/Default",
            "GUI/Text Shader",
            "Custom/Props/Prop/Default",
        };

        /// <summary>
        /// Text keeps Unity's font material (GUI/Text Shader): in Cities' render setup a
        /// depth-tested UI/Default material drew nothing at all (verified with an
        /// in-game capture). Occlusion is handled by <see cref="UpdateVisibility"/>
        /// (facing + distance) instead. Logs which built-in shaders this build has.
        /// </summary>
        private static Material CreateDepthTestedMaterial(Font font)
        {
            var found = new List<string>();
            foreach (string name in ProbeShaders)
            {
                if (Shader.Find(name) != null)
                {
                    found.Add(name);
                }
            }

            ModLog.Info("ticker: shaders available: " + string.Join(", ", found.ToArray()));
            return null;
        }

        /// <summary>
        /// Unlit board panel in the ticker/board color, so it stays dark day and night like
        /// the vanilla (self-lit) boards instead of washing out in sunlight. Unlit/Transparent
        /// has no tint, so it samples a 1×1 board-colored texture; it is drawn in the
        /// geometry queue, where depth testing works in Cities' render setup. Falls back to
        /// the lit Legacy Diffuse panel if the shader is missing.
        /// </summary>
        private static Material CreatePanelMaterial()
        {
            // color/ticker/board (#1E303E) from the Figma tokens.
            var board = new Color32(0x1E, 0x30, 0x3E, 0xFF);
            Shader unlit = Shader.Find("Unlit/Transparent");
            if (unlit != null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, board);
                texture.Apply();
                var material = new Material(unlit);
                material.name = "FinanceOverhaulTickerPanel";
                material.mainTexture = texture;
                material.renderQueue = 2000;
                ModLog.Info("ticker: panel material=Unlit/Transparent (unlit, geometry queue)");
                return material;
            }

            foreach (string name in new[] { "Legacy Shaders/Diffuse", "Diffuse" })
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    continue;
                }

                var material = new Material(shader);
                material.name = "FinanceOverhaulTickerPanel";
                material.color = board;
                ModLog.Info("ticker: panel material=" + name + " (lit fallback)");
                return material;
            }

            ModLog.Warn("ticker: no panel shader available; boards drawn without panels");
            return null;
        }

        /// <summary>One group per Stock Exchange: centre, left and right boards.</summary>
        private GameObject CreateBoard(ushort id)
        {
            var group = new GameObject("FO Ticker " + id);
            group.transform.SetParent(transform, false);
            for (int b = 0; b < BoardCentres.Length; b++)
            {
                var board = new GameObject("Board" + b);
                board.transform.SetParent(group.transform, false);
                AddRows(board);
            }

            // Backing panels replace the hidden vanilla boards (children after the boards,
            // so row and fit code only ever touches text).
            for (int b = 0; b < BoardCentres.Length; b++)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panel.name = "Panel" + b;
                Collider collider = panel.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                panel.transform.SetParent(group.transform, false);
                panel.transform.localScale = new Vector3(BoardWidth, BoardHeight, 1f);
                Renderer renderer = panel.GetComponent<Renderer>();
                if (renderer != null && panelMaterial != null)
                {
                    renderer.sharedMaterial = panelMaterial;
                }
            }

            return group;
        }

        private void AddRows(GameObject board)
        {
            for (int r = 0; r < 3; r++)
            {
                var row = new GameObject("Row" + r);
                row.transform.SetParent(board.transform, false);
                row.transform.localPosition = new Vector3(0f, (1 - r) * RowSpacing, 0f);
                TextMesh text = row.AddComponent<TextMesh>();
                text.font = font;
                text.fontSize = FontSize;
                text.characterSize = RowHeight * 10f / FontSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.richText = true;
                text.color = Color.white;
                MeshRenderer renderer = row.GetComponent<MeshRenderer>();
                if (renderer != null && font != null)
                {
                    renderer.sharedMaterial = textMaterial ?? font.material;
                }
            }
        }

        private void Place(GameObject group, ref Building b, BuildingInfo info, ushort id)
        {
            Vector3 size = info.m_size;
            var front = new Vector3(-Mathf.Sin(b.m_angle), 0f, Mathf.Cos(b.m_angle));
            group.transform.position = b.m_position;
            // TextMesh is readable when looking along its forward axis, so the group faces
            // into the building; local −Z is therefore toward the street.
            group.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            // The group faces into the building (TextMesh reads along +forward), which is a
            // 180° turn about Y, so building (x, y, z) is group (−x, y, −z).
            for (int i = 0; i < BoardCentres.Length; i++)
            {
                Vector3 c = BoardCentres[i];
                group.transform.GetChild(i).localPosition = new Vector3(-c.x, c.y, -c.z);
                if (group.transform.childCount > BoardCentres.Length + i)
                {
                    // Panel in the text plane (PanelBehindText = 0), in front of the old board face.
                    group.transform.GetChild(BoardCentres.Length + i).localPosition = new Vector3(
                        -c.x,
                        c.y,
                        -(c.z - PanelBehindText)
                    );
                }
            }

            if (!placementLogged)
            {
                LogAssetParts(info);
                placementLogged = true;
                ModLog.Info(
                    "ticker: boards for building "
                        + id
                        + " origin="
                        + b.m_position
                        + " angle="
                        + b.m_angle.ToString("0.###")
                        + " meshSize="
                        + size
                        + " centre="
                        + group.transform.GetChild(0).position
                );
            }
        }

        /// <summary>
        /// One-time diagnostic: the vanilla ticker boards may be props or sub-meshes with
        /// their own (scrolling) material. Their local positions would give exact board
        /// placement and a way to hide the baked boards later.
        /// </summary>
        private static void LogAssetParts(BuildingInfo info)
        {
            try
            {
                ModLog.Info(
                    "ticker: asset '"
                        + info.name
                        + "' material="
                        + MaterialName(info.m_material)
                        + " props="
                        + (info.m_props == null ? 0 : info.m_props.Length)
                        + " subMeshes="
                        + (info.m_subMeshes == null ? 0 : info.m_subMeshes.Length)
                );
                if (info.m_props != null)
                {
                    for (int i = 0; i < info.m_props.Length; i++)
                    {
                        BuildingInfo.Prop p = info.m_props[i];
                        PropInfo prop = p.m_finalProp ?? p.m_prop;
                        ModLog.Info(
                            "ticker:  prop["
                                + i
                                + "] "
                                + (
                                    prop != null
                                        ? prop.name
                                        : (p.m_tree != null ? "tree " + p.m_tree.name : "null")
                                )
                                + " pos="
                                + p.m_position
                                + " angle="
                                + p.m_angle.ToString("0.#")
                                + (
                                    prop != null
                                        ? " material=" + MaterialName(prop.m_material)
                                        : string.Empty
                                )
                                + (
                                    prop != null && prop.m_mesh != null
                                        ? " meshBounds=" + prop.m_mesh.bounds.size
                                        : string.Empty
                                )
                        );
                    }
                }

                if (info.m_subMeshes != null)
                {
                    for (int i = 0; i < info.m_subMeshes.Length; i++)
                    {
                        BuildingInfo.MeshInfo m = info.m_subMeshes[i];
                        BuildingInfoBase sub = m.m_subInfo;
                        ModLog.Info(
                            "ticker:  subMesh["
                                + i
                                + "] "
                                + (sub != null ? sub.name : "null")
                                + " pos="
                                + m.m_position
                                + " angle="
                                + m.m_angle.ToString("0.#")
                                + (
                                    sub != null
                                        ? " material=" + MaterialName(sub.m_material)
                                        : string.Empty
                                )
                                + (
                                    sub != null && sub.m_mesh != null
                                        ? " meshBounds="
                                            + sub.m_mesh.bounds.center
                                            + "/"
                                            + sub.m_mesh.bounds.size
                                        : string.Empty
                                )
                        );
                        if (sub != null && sub.m_mesh != null && sub.m_mesh.isReadable)
                        {
                            Vector3[] v = sub.m_mesh.vertices;
                            Vector2[] uv = sub.m_mesh.uv;
                            var sb = new System.Text.StringBuilder();
                            for (int k = 0; k < v.Length && k < 24; k++)
                            {
                                sb.Append(" [")
                                    .Append(v[k].x.ToString("0.0"))
                                    .Append(',')
                                    .Append(v[k].y.ToString("0.0"))
                                    .Append(',')
                                    .Append(v[k].z.ToString("0.0"))
                                    .Append(" uv ")
                                    .Append(
                                        uv != null && k < uv.Length
                                            ? uv[k].x.ToString("0.00")
                                                + ","
                                                + uv[k].y.ToString("0.00")
                                            : "-"
                                    )
                                    .Append(']');
                            }

                            ModLog.Info(
                                "ticker:   verts="
                                    + v.Length
                                    + " tris="
                                    + (sub.m_mesh.triangles.Length / 3)
                                    + sb
                            );
                        }
                        else if (sub != null && sub.m_mesh != null)
                        {
                            ModLog.Info("ticker:   mesh not readable");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: asset part log failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Shrinks a row that would run into the board padding. Measured in world space
        /// (boards are rotated with the building): for a flat row rotated about Y the
        /// world AABB gives width = √(x² + z²).
        /// </summary>
        private static void FitWidth(TextMesh text)
        {
            Renderer renderer = text.GetComponent<Renderer>();
            if (renderer == null || string.IsNullOrEmpty(text.text))
            {
                return;
            }

            Vector3 world = renderer.bounds.size;
            float width = Mathf.Sqrt(world.x * world.x + world.z * world.z);
            if (width > MaxRowWidth && width > 0f)
            {
                text.characterSize *= MaxRowWidth / width;
            }
        }

        private static string MaterialName(Material m)
        {
            if (m == null)
            {
                return "null";
            }

            return m.name + "/" + (m.shader != null ? m.shader.name : "?");
        }

        private static void SetRows(GameObject group, string[] rows)
        {
            for (int g = 0; g < BoardCentres.Length && g < group.transform.childCount; g++)
            {
                Transform board = group.transform.GetChild(g);
                for (int r = 0; r < board.childCount; r++)
                {
                    TextMesh text = board.GetChild(r).GetComponent<TextMesh>();
                    string value =
                        rows == null
                            ? (
                                r == 1
                                    ? "<color=#9FB0C0>Waiting for market data…</color>"
                                    : string.Empty
                            )
                        : r < rows.Length ? rows[r]
                        : string.Empty;
                    if (text.text != value)
                    {
                        text.text = value;
                        text.characterSize = RowHeight * 10f / FontSize;
                    }
                }
            }
        }

        private static void Fit(GameObject group)
        {
            for (int g = 0; g < BoardCentres.Length && g < group.transform.childCount; g++)
            {
                Transform board = group.transform.GetChild(g);
                if (!board.gameObject.activeInHierarchy)
                {
                    continue; // hidden renderers report empty bounds at the origin
                }

                for (int r = 0; r < board.childCount; r++)
                {
                    FitWidth(board.GetChild(r).GetComponent<TextMesh>());
                }

                FitHeight(board);
            }
        }

        /// <summary>
        /// Centres the board's rows on their visible glyph extent (TextMesh centres the
        /// line box, so descenders and parentheses pushed the block low) and shrinks
        /// them if taller than the padded board. Uses current world bounds, so the
        /// correction converges in one step.
        /// </summary>
        private static void FitHeight(Transform board)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            int counted = 0;
            for (int r = 0; r < board.childCount; r++)
            {
                Transform row = board.GetChild(r);
                TextMesh text = row.GetComponent<TextMesh>();
                Renderer renderer = row.GetComponent<Renderer>();
                if (renderer == null || text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                Bounds b = renderer.bounds;
                min = Mathf.Min(min, b.min.y);
                max = Mathf.Max(max, b.max.y);
                counted++;
            }

            if (counted == 0)
            {
                return;
            }

            float height = max - min;
            if (height > InnerHeight && height > 0f)
            {
                float shrink = InnerHeight / height;
                for (int r = 0; r < board.childCount; r++)
                {
                    board.GetChild(r).GetComponent<TextMesh>().characterSize *= shrink;
                }
            }

            float delta = board.position.y - (min + max) * 0.5f;
            if (Mathf.Abs(delta) > 0.01f)
            {
                for (int r = 0; r < board.childCount; r++)
                {
                    Transform row = board.GetChild(r);
                    row.position += new Vector3(0f, delta, 0f);
                }
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
        }
    }
}
#endif
