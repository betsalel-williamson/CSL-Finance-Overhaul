using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// The local company cards in Economy → Investments → Purchasable Investments (Figma UI Map
    /// band 16): above the game's stocks, a section label, one card per kind of business the
    /// city has (where it stands, and a bar towards the unlock or the next listing slot), and a
    /// line summing up the board. In the look of the game's own locked stocks. Shown while
    /// Options → Local IPOs is on. Lives on the overlay root (hot reload removes it); stale
    /// cards are removed by name.
    /// </summary>
    public class CompanyCards : MonoBehaviour
    {
        public const string Prefix = "FinanceOverhaulCompanyCard";
        public const string SectionName = Prefix + "Section";
        public const string FooterName = Prefix + "Footer";
        public const string TextSuffix = "Text"; // not shown: a control name
        public const string CardPrefix = Prefix + "-";
        public const string LinePrefix = Prefix + "Line-";
        public const string NotePrefix = Prefix + "Note-";
        public const string BarTextPrefix = Prefix + "BarText-";

        private const float CardHeight = 96f;
        private const float BarWidth = 250f;
        private const float BarHeight = 14f;
        private static readonly Color32 CardTint = new Color32(107, 118, 135, 255); // the game's stock cards
        private static readonly Color32 Text = FoTheme.GameText;
        private static readonly Color32 Value = FoTheme.GameValue;
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Held = FoTheme.GameNegative;
        private static readonly Color32 Bar = FoTheme.GamePositive;
        private static readonly Color32 BarTrack = new Color32(8, 12, 18, 255);

        private EconomyPanel economy;
        private string shown;
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                if (economy == null)
                {
                    economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                }

                if (economy == null || !economy.isInvestmentOpen)
                {
                    return;
                }

                UIComponent list = economy.component.Find("InvestmentsScrollablePanel");
                if (list == null)
                {
                    return;
                }

                if (!CompanyDesk.Enabled)
                {
                    if (shown != null)
                    {
                        Clear(list);
                        shown = null;
                    }

                    return;
                }

                BoardView board = CompanyDesk.Board();
                IpoRules rules = CompanyDesk.Rules ?? new IpoRules();
                string next = SimulationManager
                    .instance.m_currentGameTime.AddDays(board.NextAllowedDay - OrderDesk.Today)
                    .ToString("dd/MM/yyyy");
                string signature = Signature(board, rules, next);
                if (signature != shown || list.Find(SectionName) == null)
                {
                    Clear(list);
                    Build(list, board, rules, next);
                    shown = signature;
                }

                Arrange(list);
            }
            catch (Exception ex)
            {
                ModLog.Warn("company cards: update failed: " + ex.Message);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (economy != null)
                {
                    UIComponent list = economy.component.Find("InvestmentsScrollablePanel");
                    if (list != null)
                    {
                        Clear(list);
                    }
                }
            }
            catch
            {
                // fail soft on unload
            }
        }

        /// <summary>
        /// Keeps the section, the cards and the footer above the game's stocks, in that order.
        /// The game renumbers its own items when it sorts them; called after that too (postfix
        /// on EconomyPanel.UpdateInvestments).
        /// </summary>
        public static void Arrange(UIComponent list)
        {
            if (list == null)
            {
                return;
            }

            var ours = new List<UIComponent>();
            var theirs = new List<UIComponent>();
            foreach (UIComponent c in list.components)
            {
                if (c != null)
                {
                    (IsOurs(c) ? ours : theirs).Add(c);
                }
            }

            if (ours.Count == 0)
            {
                return;
            }

            ours.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            int z = 0;
            foreach (UIComponent c in ours)
            {
                if (c.zOrder != z)
                {
                    c.zOrder = z;
                }

                z++;
            }

            foreach (UIComponent c in theirs)
            {
                if (c.zOrder != z)
                {
                    c.zOrder = z;
                }

                z++;
            }
        }

        /// <summary>Postfix target: EconomyPanel.UpdateInvestments.</summary>
        public static void Arrange(EconomyPanel panel)
        {
            Arrange(panel == null ? null : panel.component.Find("InvestmentsScrollablePanel"));
        }

        private static bool IsOurs(UIComponent c)
        {
            return c.name != null && c.name.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>The section label first, the cards in the order built, the footer last.</summary>
        private static int Rank(UIComponent c)
        {
            if (c.name == SectionName)
            {
                return -1;
            }

            return c.name == FooterName ? int.MaxValue : (int)(BusinessKind)c.objectUserData;
        }

        private static void Clear(UIComponent list)
        {
            UiCleanup.RemoveChildrenWhere(list, IsOurs);
        }

        private static string Signature(BoardView board, IpoRules rules, string next)
        {
            var sb = new StringBuilder();
            sb.Append(IpoCardText.Footer(board, rules)).Append('|');
            foreach (KindStatus k in board.Kinds)
            {
                sb.Append(k.Kind)
                    .Append(':')
                    .Append(IpoCardText.Line(k))
                    .Append(':')
                    .Append(IpoCardText.Note(k, board, next))
                    .Append(':')
                    .Append(IpoCardText.BarText(k))
                    .Append('|');
            }

            return sb.ToString();
        }

        private void Build(UIComponent list, BoardView board, IpoRules rules, string next)
        {
            UIFont font = FontOf(list);
            float width = list.width;
            UIPanel section = list.AddUIComponent<UIPanel>();
            section.name = SectionName;
            section.size = new Vector2(width, 22f);
            UILabel heading = Label(
                section,
                IpoCardText.Section,
                0.875f,
                FoTheme.GameHeading,
                font
            );
            heading.name = SectionName + TextSuffix;
            heading.relativePosition = new Vector3(8f, 3f);

            foreach (KindStatus status in board.Kinds)
            {
                BuildCard(list, status, board, next, font, width);
            }

            UIPanel footer = list.AddUIComponent<UIPanel>();
            footer.name = FooterName;
            footer.size = new Vector2(width, 24f);
            // The line can be longer than the list is wide: it wraps, and the row grows with it.
            UILabel sum = Label(footer, string.Empty, 0.7f, Muted, font);
            sum.name = FooterName + TextSuffix;
            sum.autoSize = false;
            sum.wordWrap = true;
            sum.autoHeight = true;
            sum.width = width - 16f;
            sum.text = IpoCardText.Footer(board, rules);
            sum.relativePosition = new Vector3(8f, 4f);
            footer.height = sum.height + 10f;
        }

        private void BuildCard(
            UIComponent list,
            KindStatus status,
            BoardView board,
            string next,
            UIFont font,
            float width
        )
        {
            UIPanel card = list.AddUIComponent<UIPanel>();
            card.name = CardPrefix + status.Kind;
            card.objectUserData = status.Kind;
            card.size = new Vector2(width, CardHeight);
            card.backgroundSprite = FoTheme.GameLoanCardSprite; // the stock cards' own sprite
            card.color = CardTint;

            UILabel title = Label(card, IpoCardText.Title(status.Kind), 1.0f, Text, font);
            title.relativePosition = new Vector3((width - title.width) / 2f, 8f);

            UILabel line = Label(
                card,
                IpoCardText.Line(status),
                0.8125f,
                status.State == KindState.Locked ? Muted : Value,
                font
            );
            line.name = LinePrefix + status.Kind;
            line.relativePosition = new Vector3((width - line.width) / 2f, 32f);

            string noteText = IpoCardText.Note(status, board, next);
            if (noteText.Length > 0)
            {
                UILabel note = Label(
                    card,
                    noteText,
                    0.75f,
                    status.State == KindState.Paused ? Held : Muted,
                    font
                );
                note.name = NotePrefix + status.Kind;
                note.relativePosition = new Vector3((width - note.width) / 2f, 51f);
            }

            float barX = (width - BarWidth) / 2f;
            float barY = CardHeight - BarHeight - 9f;
            UILabel caption = Label(card, IpoCardText.BarCaption(status), 0.7f, Muted, font);
            caption.relativePosition = new Vector3(barX - caption.width - 8f, barY);
            UISprite track = card.AddUIComponent<UISprite>();
            track.spriteName = "EmptySprite";
            track.color = BarTrack;
            track.size = new Vector2(BarWidth, BarHeight);
            track.relativePosition = new Vector3(barX, barY);
            UISprite fill = card.AddUIComponent<UISprite>();
            fill.spriteName = "EmptySprite";
            fill.color = Bar;
            fill.size = new Vector2(
                Mathf.Max(2f, BarWidth * IpoCardText.BarFraction(status)),
                BarHeight
            );
            fill.relativePosition = new Vector3(barX, barY);
            UILabel barText = Label(card, IpoCardText.BarText(status), 0.7f, Text, font);
            barText.name = BarTextPrefix + status.Kind;
            barText.relativePosition = new Vector3(barX + BarWidth + 8f, barY);
        }

        private static UIFont FontOf(UIComponent list)
        {
            UILabel any = list.GetComponentInChildren<UILabel>();
            return any == null ? null : any.font;
        }

        private static UILabel Label(
            UIComponent parent,
            string text,
            float scale,
            Color32 colour,
            UIFont font
        )
        {
            UILabel l = parent.AddUIComponent<UILabel>();
            if (font != null)
            {
                l.font = font;
            }

            l.textScale = scale;
            l.textColor = colour;
            l.text = text;
            return l;
        }
    }
}
