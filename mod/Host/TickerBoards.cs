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
        // First-guess placement (calibrate from screenshots).
        internal const float FrontOffset = 0.6f; // metres in front of the mesh's front face
        internal const float HeightFraction = 0.45f; // of the mesh height
        internal const float RowHeight = 1.6f; // metres per row
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
                font = Font.CreateDynamicFontFromOSFont(SerifFonts, FontSize);
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                ModLog.Info("ticker: board font=" + (font == null ? "none" : font.name));
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

        private GameObject CreateBoard(ushort id)
        {
            var board = new GameObject("FO Ticker Board " + id);
            board.transform.SetParent(transform, false);
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

            return board;
        }

        private void Place(GameObject board, ref Building b, BuildingInfo info, ushort id)
        {
            Vector3 size = info.m_size;
            var local = new Vector3(0f, size.y * HeightFraction, size.z * 0.5f + FrontOffset);
            Vector3 pos = Building.CalculatePosition(b.m_position, b.m_angle, local);
            var front = new Vector3(-Mathf.Sin(b.m_angle), 0f, Mathf.Cos(b.m_angle));
            board.transform.position = pos;
            // TextMesh is readable when looking along its forward axis, so face into the building.
            board.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            if (!placementLogged)
            {
                placementLogged = true;
                ModLog.Info(
                    "ticker: board for building "
                        + id
                        + " at "
                        + pos
                        + " angle="
                        + b.m_angle.ToString("0.###")
                        + " meshSize="
                        + size
                        + " cells="
                        + info.m_cellWidth
                        + "x"
                        + info.m_cellLength
                        + " local="
                        + local
                );
            }
        }

        private static void SetRows(GameObject board, string[] rows)
        {
            for (int r = 0; r < 3; r++)
            {
                Transform row = board.transform.GetChild(r);
                TextMesh text = row.GetComponent<TextMesh>();
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
