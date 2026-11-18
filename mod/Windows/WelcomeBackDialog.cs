using System;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Welcome back (Figma band 14, docs/features/save-compatibility.md): shown once after a city
    /// is loaded with the mod when bond payments fell due while the mod was off. Two choices;
    /// × does the default, "Start fresh from today". Answering settles the book through
    /// <see cref="BondDesk.SettleMissed"/>; saving without answering keeps the question for the
    /// next load.
    /// </summary>
    public class WelcomeBackDialog : MonoBehaviour
    {
        private const string PanelName = "FinanceOverhaulWelcomeBack";
        private const float Width = 460f;
        private const float Pad = 20f;

        private static readonly Color32 Frame = FoTheme.Dialog;
        private static readonly Color32 Header = FoTheme.Header;
        private static readonly Color32 Card = FoTheme.PanelAlt;
        private static readonly Color32 White = FoTheme.TextPrimary;
        private static readonly Color32 Body = FoTheme.TextLabel;
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Good = FoTheme.Accent;

        private UIPanel panel;
        private float nextCheck;

        public static WelcomeBackDialog Current { get; private set; }

        public bool IsShown
        {
            get { return panel != null && panel.isVisible; }
        }

        private void Awake()
        {
            Current = this;
        }

        private void Update()
        {
            if (panel != null || Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                MissedPayments m = BondDesk.Pending;
                if (m != null)
                {
                    Build(m);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("welcome back: show failed: " + ex);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        /// <summary>The player's answer (also used by game UI tests).</summary>
        public void Choose(bool pay)
        {
            BondDesk.SettleMissed(pay);
            Close();
        }

        /// <summary>Game UI tests: clicks × like the player.</summary>
        public void ClickClose()
        {
            UIButton close = panel == null ? null : panel.Find<UIButton>(PanelName + "Close");
            if (close != null)
            {
                close.SimulateClick();
            }
        }

        private void Close()
        {
            if (panel != null)
            {
                Destroy(panel.gameObject);
                panel = null;
            }

            nextCheck = Time.unscaledTime + 1f;
        }

        private void OnDestroy()
        {
            try
            {
                if (panel != null)
                {
                    Destroy(panel.gameObject);
                }
            }
            catch
            {
                // fail soft on unload
            }

            if (Current == this)
            {
                Current = null;
            }
        }

        private void Build(MissedPayments m)
        {
            UIView view = UIView.GetAView();
            UIComponent stale = view.FindUIComponent(PanelName);
            if (stale != null)
            {
                Destroy(stale.gameObject);
            }

            panel = (UIPanel)view.AddUIComponent(typeof(UIPanel));
            panel.name = PanelName;
            panel.backgroundSprite = "GenericPanelWhite";
            panel.color = Frame;
            panel.width = Width;

            UIPanel header = panel.AddUIComponent<UIPanel>();
            header.backgroundSprite = "GenericPanelWhite";
            header.color = Header;
            header.size = new Vector2(Width, 36f);
            header.relativePosition = Vector3.zero;
            UILabel title = Label(header, WelcomeText.Title, 1.1f, White, Width - 60f);
            title.relativePosition = new Vector3(12f, 8f);

            // × does the default: start fresh from today.
            UIButton close = header.AddUIComponent<UIButton>();
            close.name = PanelName + "Close";
            close.size = new Vector2(30f, 30f);
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.relativePosition = new Vector3(Width - 34f, 3f);
            close.tooltip = WelcomeText.CloseTip;
            close.eventClick += (c, p) => Choose(false);

            float y = 48f;
            float inner = Width - 2 * Pad;
            y = Place(Label(panel, WelcomeText.Lead(m.DaysAway), 0.875f, White, inner), y, 8f);
            y = Place(Label(panel, WelcomeText.Ask, 0.8125f, Body, inner), y, 12f);

            y = Choice(
                y,
                WelcomeText.Pay(m.TotalCents),
                WelcomeText.PayHelp(m.DaysAway),
                36f,
                true
            );
            y = Choice(y, WelcomeText.Skip, WelcomeText.SkipHelp, 26f, false);
            if (m.MaturedCents > 0)
            {
                y = Place(
                    Label(panel, WelcomeText.Matured(m.MaturedCents), 0.75f, Body, inner),
                    y,
                    8f
                );
            }

            if (IndexListing.RestoredShares > 0)
            {
                y = Place(
                    Label(
                        panel,
                        WelcomeText.IndexBack(IndexListing.RestoredShares),
                        0.75f,
                        Good,
                        inner
                    ),
                    y,
                    8f
                );
            }

            y = Place(Label(panel, WelcomeText.Footer, 0.6875f, Muted, inner), y, 0f);
            panel.height = y + 16f;
            FoWindow.CenterOnScreen(panel);
            panel.BringToFront();
            ModLog.Info("welcome back: shown (" + m.DaysAway + " day(s) away)");
        }

        private float Choice(float y, string button, string help, float buttonHeight, bool main)
        {
            float inner = Width - 2 * Pad;
            UIPanel card = panel.AddUIComponent<UIPanel>();
            card.backgroundSprite = "GenericPanelWhite";
            card.color = Card;
            card.width = inner;
            card.relativePosition = new Vector3(Pad, y);

            UIButton b = card.AddUIComponent<UIButton>();
            b.text = button;
            b.textScale = main ? 0.875f : 0.8125f;
            b.normalBgSprite = "ButtonMenu";
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            b.textColor = White;
            b.autoSize = true;
            b.textPadding = new RectOffset(14, 14, main ? 10 : 5, main ? 8 : 4);
            b.autoSize = false;
            b.height = buttonHeight;
            b.relativePosition = new Vector3(12f, 10f);
            b.eventClick += (c, p) => Choose(main);

            UILabel h = Label(card, help, 0.75f, Muted, inner - 24f);
            h.relativePosition = new Vector3(12f, 10f + buttonHeight + 6f);
            card.height = h.relativePosition.y + h.height + 10f;
            return y + card.height + 10f;
        }

        private float Place(UILabel l, float y, float gapAfter)
        {
            l.relativePosition = new Vector3(Pad, y);
            return y + l.height + gapAfter;
        }

        private static UILabel Label(
            UIComponent parent,
            string text,
            float scale,
            Color32 colour,
            float width
        )
        {
            UILabel l = parent.AddUIComponent<UILabel>();
            l.textScale = scale;
            l.textColor = colour;
            l.wordWrap = true;
            l.autoSize = false;
            l.autoHeight = true;
            l.width = width;
            l.text = text;
            return l;
        }
    }
}
