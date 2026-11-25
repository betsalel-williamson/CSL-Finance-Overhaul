using System; // Func
// ColossalFramework.UI: UIButton, UIComponent, UIHorizontalAlignment, UILabel, UIPanel,
//   UITextField, UIVerticalAlignment, UIView
using ColossalFramework.UI;
using UnityEngine; // Mathf, RectOffset, Screen, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// A small "name this" dialog: a title, a text field and OK / Cancel (after Trackpad Camera
    /// Control's Save as… dialog). Opened over the Options window, it sits inside that window's
    /// modal so it takes clicks without a second modal. OK (or Enter) hands the name to the
    /// caller, which says whether it was accepted; the dialog closes when it was. Knows no
    /// feature: the caller supplies every word.
    /// </summary>
    internal static class FoNameDialog
    {
        private const float Width = 360f;
        private const float Height = 124f;
        private const string PanelName = "FinanceOverhaulNameDialog"; // not shown: a control name

        private static UIPanel dialog;
        private static UITextField field;
        private static bool modalPushed;

        /// <summary>The open dialog's text field, for game UI tests (null when closed).</summary>
        internal static UITextField Field
        {
            get { return dialog == null ? null : field; }
        }

        /// <summary>Opens the dialog; <paramref name="accept"/> returns false to keep it open.</summary>
        public static void Show(
            string title,
            string suggested,
            string ok,
            string cancel,
            Func<string, bool> accept
        )
        {
            Close();
            UIView view = UIView.GetAView();
            if (view == null)
            {
                return;
            }

            UIComponent host = UIView.GetModalComponent();
            if (host != null)
            {
                dialog = host.AddUIComponent<UIPanel>();
                modalPushed = false;
            }
            else
            {
                dialog = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
                if (dialog == null)
                {
                    return;
                }

                UIView.PushModal(dialog);
                modalPushed = true;
            }

            dialog.name = PanelName;
            dialog.backgroundSprite = "MenuPanel2"; // not shown: a game sprite name
            dialog.size = new Vector2(Width, Height);
            float uiW = view.fixedWidth > 1f ? view.fixedWidth : Screen.width;
            float uiH = view.fixedHeight > 1f ? view.fixedHeight : Screen.height;
            dialog.absolutePosition = new Vector3(
                Mathf.Floor((uiW - Width) * 0.5f),
                Mathf.Floor((uiH - Height) * 0.5f)
            );
            dialog.canFocus = true;
            dialog.isInteractive = true;
            dialog.BringToFront();

            UILabel heading = dialog.AddUIComponent<UILabel>();
            heading.text = title;
            heading.textColor = FoTheme.GameText;
            heading.relativePosition = new Vector3(12f, 10f);

            field = dialog.AddUIComponent<UITextField>();
            field.name = PanelName + "Field"; // not shown: a control name
            field.size = new Vector2(Width - 24f, 26f);
            field.relativePosition = new Vector3(12f, 38f);
            field.normalBgSprite = FoTheme.GameFieldSprite;
            field.hoveredBgSprite = FoTheme.GameFieldSprite;
            field.focusedBgSprite = FoTheme.GameFieldSprite;
            field.selectionSprite = "EmptySprite"; // not shown: a game sprite name
            field.color = FoTheme.Field;
            field.textColor = FoTheme.GameText;
            field.padding = new RectOffset(6, 6, 5, 3);
            field.horizontalAlignment = UIHorizontalAlignment.Left;
            field.verticalAlignment = UIVerticalAlignment.Middle;
            field.cursorBlinkTime = 0.45f;
            field.cursorWidth = 1;
            field.text = suggested ?? string.Empty;
            field.selectOnFocus = true;
            field.readOnly = false;
            field.canFocus = true;
            field.isInteractive = true;
            // A programmatic field takes no typing without this (the template's fields have it).
            field.builtinKeyNavigation = true;
            // A focus flicker on open would submit and close the dialog.
            field.submitOnFocusLost = false;
            field.eventTextSubmitted += (c, t) => TryAccept(accept);

            UIButton no = FoButton.Add(
                dialog,
                PanelName + "Cancel", // not shown: a control name
                cancel,
                new Vector2(100f, 28f),
                0.85f
            );
            no.relativePosition = new Vector3(12f, 80f);
            no.eventClick += (c, e) =>
            {
                e.Use();
                Close();
            };

            UIButton yes = FoButton.Add(
                dialog,
                PanelName + "Ok", // not shown: a control name
                ok,
                new Vector2(100f, 28f),
                0.85f
            );
            yes.relativePosition = new Vector3(Width - 112f, 80f);
            yes.eventClick += (c, e) =>
            {
                e.Use();
                TryAccept(accept);
            };

            dialog.Focus();
            field.Focus();
        }

        private static void TryAccept(Func<string, bool> accept)
        {
            string name = field == null ? string.Empty : field.text;
            if (accept != null && accept(name))
            {
                Close();
            }
        }

        /// <summary>Game UI tests: OK with the dialog's own handler.</summary>
        internal static bool TestOkOpen()
        {
            if (dialog == null)
            {
                return false;
            }

            UIButton yes = dialog.Find<UIButton>(PanelName + "Ok");
            if (yes == null)
            {
                return false;
            }

            yes.SimulateClick();
            return true;
        }

        public static bool IsOpen
        {
            get { return dialog != null; }
        }

        public static void Close()
        {
            if (dialog != null)
            {
                if (modalPushed && UIView.GetModalComponent() == dialog)
                {
                    UIView.PopModal();
                }

                UnityEngine.Object.Destroy(dialog.gameObject);
            }

            dialog = null;
            field = null;
            modalPushed = false;
        }
    }
}
