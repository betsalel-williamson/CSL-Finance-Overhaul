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
            new Vector3(0f, 16.9f, 23.7f), // centre (Submesh01), 0.1 m in front of the face
            new Vector3(-13.15f, 12.45f, 23.7f), // left (Submesh02)
            new Vector3(13.15f, 12.45f, 23.7f), // right (Submesh03)
        };
        internal const float BoardWidth = 15.3f;
        internal const float MaxRowWidth = 14.8f; // keep text inside the board edges
        internal const float RowSpacing = 1.1f; // 3 rows across the 3.4 m board
        internal const float RowHeight = 0.9f; // metres per row of text
        internal const int FontSize = 64;
        internal const float RefreshSeconds = 1f;

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
        private float nextRefresh;
        private bool placementLogged;

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + RefreshSeconds;
            try
            {
                bool enabled = Mod.Settings == null || Mod.Settings.LiveTickerEnabled;
                if (!enabled)
                {
                    foreach (GameObject b in boards.Values)
                    {
                        b.SetActive(false);
                    }

                    return;
                }

                Refresh();
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: overlay refresh failed: " + ex.Message);
                enabled = false; // stop this component; vanilla boards remain
            }
        }

        private void Refresh()
        {
            if (font == null)
            {
                font = PickFont();
            }

            string[] rows = TickerFeed.CurrentRows;
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
                GameObject board;
                if (!boards.TryGetValue(id, out board) || board == null)
                {
                    board = CreateBoard(id);
                    boards[id] = board;
                }

                board.SetActive(true);
                Place(board, ref buffer[i], info, id);
                SetRows(board, rows);
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
                    renderer.sharedMaterial = font.material;
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

        /// <summary>Shrinks a row that would run past the vanilla board's edges.</summary>
        private static void FitWidth(TextMesh text)
        {
            MeshFilter filter = text.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                return;
            }

            float width = mesh.bounds.size.x;
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
            for (int g = 0; g < group.transform.childCount; g++)
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

                    FitWidth(text);
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
