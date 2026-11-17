using System;
using System.Collections.Generic;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>LED ticker: building the board meshes and materials on a Stock Exchange and painting the dots.</summary>
    public partial class TickerOverlay
    {
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

        /// <summary>One marquee row with its ring texture (readable, so columns can be repainted).</summary>
        private void CreateRow(int r)
        {
            var marquee = new LedMarquee(ScreenDots, r == 1 ? Strings.Get("ticker.waiting") : null);
            marquee.Display = DisplayNow(); // the first screen already in the chosen style
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
        /// <summary>Rebuilds the dot colours when Options → LED brightness changed; true if it did.</summary>
        private bool UpdatePalette()
        {
            LedBrightness level =
                Mod.Settings != null ? Mod.Settings.TickerBrightness : LedBrightness.Bright;
            if (palette != null && level == paletteLevel)
            {
                return false;
            }

            var colours = new Color32[LedPalette.Count];
            var gapColours = new Color32[LedPalette.Count];
            for (int i = 0; i < colours.Length; i++)
            {
                LedRgb c = LedPalette.Colour((byte)i, level);
                colours[i] = new Color32(c.R, c.G, c.B, 0xFF);
                LedRgb g = LedPalette.Gap((byte)i, level);
                gapColours[i] = new Color32(g.R, g.G, g.B, 0xFF);
            }

            gaps = gapColours;

            bool changed = palette != null;
            palette = colours;
            paletteLevel = level;
            return changed;
        }

        private void Paint(int r, long from, long to)
        {
            if (palette == null)
            {
                UpdatePalette();
            }

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
                    Color32 lit = cell < palette.Length ? palette[cell] : palette[0];
                    Color32 gapColour = cell < gaps.Length ? gaps[cell] : gaps[0];
                    for (int ty = 0; ty < t; ty++)
                    {
                        int texY = (strip.Height - 1 - dy) * t + (t - 1 - ty);
                        for (int tx = 0; tx < t; tx++)
                        {
                            bool gap = tx == t - 1 || ty == t - 1;
                            pixels[texY * w + dx * t + tx] = gap ? gapColour : lit;
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
