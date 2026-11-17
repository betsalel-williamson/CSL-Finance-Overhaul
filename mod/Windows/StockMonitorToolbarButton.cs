using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>Stock Monitor: the round button in the top-left mod row that opens the window.</summary>
    public sealed partial class StockMonitorHost
    {
        private void BuildButton()
        {
            UIView view = UIView.GetAView();
            button = (UIButton)view.AddUIComponent(typeof(UIButton));
            button.name = ButtonName;
            button.size = new Vector2(46f, 46f);
            button.normalBgSprite = "RoundBackBig";
            button.hoveredBgSprite = "RoundBackBigHovered";
            button.pressedBgSprite = "RoundBackBigPressed";
            button.focusedBgSprite = "RoundBackBig";
            button.tooltip = Strings.Get("monitor.button.tip");
            button.playAudioEvents = true;
            button.eventClick += (c, p) =>
            {
                if (p.buttons == UIMouseButton.Left)
                {
                    Toggle();
                }
            };
            button.eventMouseDown += (c, p) =>
            {
                if (p.buttons == UIMouseButton.Right)
                {
                    dragging = true;
                    lastMouse = Input.mousePosition;
                }
            };

            buttonIcon = button.AddUIComponent<UITextureSprite>();
            buttonIcon.name = Prefix + "ButtonIcon";
            buttonIcon.size = new Vector2(26f, 26f);
            buttonIcon.relativePosition = new Vector3(10f, 10f);
            buttonIcon.isInteractive = false;
            buttonIcon.texture = IconTexture();

            // Default: in the top-left mod row, right of the first two buttons.
            float x = float.IsNaN(Settings.ButtonX) ? 177f : Settings.ButtonX;
            float y = float.IsNaN(Settings.ButtonY) ? 10f : Settings.ButtonY;
            button.absolutePosition = Clamp(new Vector3(x, y), button.size);
            StyleButton(false);
        }

        private void StyleButton(bool open)
        {
            if (button == null)
            {
                return;
            }

            button.normalBgSprite = open ? "RoundBackBigPressed" : "RoundBackBig";
            button.focusedBgSprite = button.normalBgSprite;
        }

        private void DragButton()
        {
            if (!dragging)
            {
                return;
            }

            if (!Input.GetMouseButton(1))
            {
                dragging = false;
                Settings.ButtonX = button.absolutePosition.x;
                Settings.ButtonY = button.absolutePosition.y;
                Mod.SaveSettings();
                return;
            }

            // Screen pixels → UI units (UI y grows downward).
            Vector3 delta = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            float scale = UIView.GetAView().GetScreenResolution().y / Mathf.Max(1f, Screen.height);
            button.absolutePosition = Clamp(
                button.absolutePosition + new Vector3(delta.x * scale, -delta.y * scale),
                button.size
            );
        }

        private static Vector3 Clamp(Vector3 p, Vector2 size)
        {
            Vector2 res = UIView.GetAView().GetScreenResolution();
            return new Vector3(
                Mathf.Clamp(p.x, 0f, Mathf.Max(0f, res.x - size.x)),
                Mathf.Clamp(p.y, 0f, Mathf.Max(0f, res.y - size.y))
            );
        }

        private static Texture2D IconTexture()
        {
            const int s = 52;
            byte[] mask = Sparkline.Render(new[] { 2f, 5f, 4f, 7f, 6f, 10f }, s, 36, 5);
            var tex = new Texture2D(s, s, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[s * s];
            for (int y = 0; y < 36; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // texture rows go bottom-up; centre the 36-pixel line vertically
                    px[(s - 1 - (y + 8)) * s + x] =
                        mask[y * s + x] == Sparkline.Line ? Gain : FoTheme.Clear;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
