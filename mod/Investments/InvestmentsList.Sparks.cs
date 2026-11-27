using System.Collections.Generic; // Dictionary
using ColossalFramework.UI; // UITextureSprite
using FinanceOverhaul.Core; // MonitorQuote, StockMonitor
using UnityEngine; // Color32, Texture2D, Vector2

namespace FinanceOverhaul
{
    /// <summary>Each stock's own 30-day line texture and sprite, drawn only when its prices change.</summary>
    public partial class InvestmentsList
    {
        // One 30-day line texture per stock, drawn when its prices or colour change: a scroll
        // only hands a pooled line another stock's texture, it never repaints (Figma band 20).
        private readonly Dictionary<string, Texture2D> sparks = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, string> sparksDrawn = new Dictionary<string, string>();

        private readonly Dictionary<string, UITextureSprite> sparkSprites =
            new Dictionary<string, UITextureSprite>();

        /// <summary>
        /// The stock's own 30-day line sprite in the list's view, with the stock's texture: a
        /// pooled line moves it over itself. Named for the stock ("…Row-GGISpark").
        /// </summary>
        private UITextureSprite SparkSprite(MonitorQuote q)
        {
            UITextureSprite sprite;
            if (!sparkSprites.TryGetValue(q.Symbol, out sprite) || sprite == null)
            {
                sprite = view.AddUIComponent<UITextureSprite>();
                sprite.name = Prefix + "Row-" + q.Symbol + "Spark";
                sprite.size = new Vector2(Row.SparkWidth, Row.SparkHeight);
                // Over the line: the game's own stock tooltip with its large chart (the list
                // checks the pointer each frame, see Hover).
                sprite.isInteractive = false;
                sparkSprites[q.Symbol] = sprite;
            }

            sprite.texture = SparkFor(q, FoTheme.Change(StockMonitor.ChangeColour(q.ChangeRate)));
            return sprite;
        }

        /// <summary>The stock's 30-day line, redrawn only when its prices or colour changed.</summary>
        private Texture2D SparkFor(MonitorQuote q, Color32 colour)
        {
            Texture2D tex;
            if (!sparks.TryGetValue(q.Symbol, out tex) || tex == null)
            {
                tex = FoTexture.Canvas(Row.SparkTextureWidth, Row.SparkTextureHeight);
                tex.name = Prefix + "Spark-" + q.Symbol;
                sparks[q.Symbol] = tex;
                sparksDrawn.Remove(q.Symbol);
            }

            var sig = new System.Text.StringBuilder();
            sig.Append(colour.r).Append(',').Append(colour.g).Append(',').Append(colour.b);
            foreach (float v in q.History ?? new float[0])
            {
                sig.Append('|').Append(v.ToString("0.##"));
            }

            string signature = sig.ToString();
            string had;
            if (!sparksDrawn.TryGetValue(q.Symbol, out had) || had != signature)
            {
                FoSparkline.Draw(tex, q.History, colour, Row.SparkThickness);
                sparksDrawn[q.Symbol] = signature;
            }

            return tex;
        }
    }
}
