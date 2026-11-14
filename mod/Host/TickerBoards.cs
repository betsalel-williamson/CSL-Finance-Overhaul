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
        // Placement calibrated from an in-game screenshot (2026-11-13): the vanilla facade
        // has a centre board at ~9.6 m and two side boards at ~7.6 m, ~12.5 m either side,
        // set back behind the columns. Offsets are metres in the building's frame.
        internal const float Setback = 5f; // metres behind the mesh's front bound
        internal static readonly Vector3[] BoardOffsets =
        {
            new Vector3(0f, 9.6f, 0f), // centre
            new Vector3(-12.5f, 7.6f, 0f), // left
            new Vector3(12.5f, 7.6f, 0f), // right
        };
        internal const float RowHeight = 1.2f; // metres per row
        internal const int FontSize = 64;
        internal const float RefreshSeconds = 1f;

        private static readonly string[] SerifFonts =
        {
            "Goudy Old Style",
            "Hoefler Text",
            "Baskerville",
            "Georgia",
            "Times New Roman",
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
            for (int b = 0; b < BoardOffsets.Length; b++)
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
                row.transform.localPosition = new Vector3(0f, (1 - r) * RowHeight * 1.1f, 0f);
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
            float faceZ = -(size.z * 0.5f - Setback);
            for (int i = 0; i < BoardOffsets.Length; i++)
            {
                Vector3 o = BoardOffsets[i];
                group.transform.GetChild(i).localPosition = new Vector3(o.x, o.y, faceZ);
            }

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
                        + " meshSize="
                        + size
                        + " faceZ="
                        + faceZ.ToString("0.0")
                        + " centre="
                        + group.transform.GetChild(0).position
                );
            }
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
                    }
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
