using System;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// The icon of a stock the game has no art for (the Index, a local company, the dev test
    /// stock): a coin like the game's own stock coins with the ticker symbol on it, in place
    /// of the stack of money the game falls back to. The game sets every stock icon through
    /// InvestmentAI.SetSpriteName; this runs around it. The game reuses its cards, so an icon
    /// that goes back to one of the game's stocks gets its own atlas back first.
    /// </summary>
    public static class CoinIcons
    {
        public const string AtlasName = "FinanceOverhaulCoins";
        public const string SpriteName = "FinanceOverhaulCoin";
        public const string SymbolName = "FinanceOverhaulCoinSymbol";
        public const string GlyphName = "FinanceOverhaulCoinGlyph";
        private const string GameFallback = "MoneyThumb"; // not shown: a game sprite name
        private const int TextureSize = 128;

        // The game's stock coins: gold face, darker rim and side, a lighter ring.
        private static readonly Color32 FaceColour = new Color32(246, 184, 60, 255);
        private static readonly Color32 RimColour = new Color32(232, 152, 28, 255);
        private static readonly Color32 EdgeColour = new Color32(186, 112, 16, 255);
        private static readonly Color32 RingColour = new Color32(255, 214, 110, 255);
        private static readonly Color32 SymbolColour = new Color32(122, 72, 6, 255);

        private static UITextureAtlas atlas;

        /// <summary>Prefix of InvestmentAI.SetSpriteName: back to the game's atlas, no symbol.</summary>
        public static void Before(UISprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (sprite.atlas != null && sprite.atlas.name == AtlasName)
            {
                sprite.atlas = UIView.GetAView().defaultAtlas;
            }

            UIComponent symbol = sprite.Find(SymbolName);
            if (symbol != null)
            {
                symbol.isVisible = false;
            }

            UIComponent glyph = sprite.Find(GlyphName);
            if (glyph != null)
            {
                glyph.isVisible = false;
            }
        }

        /// <summary>
        /// The game's own icon for a kind of business, drawn in the middle of a local company's
        /// coin: the zone icon its building panel shows, or the icon of its specialisation. The
        /// first name the game has is used; with none, the coin carries the ticker symbol.
        /// </summary>
        private static string[] GlyphsFor(BusinessKind kind)
        {
            switch (kind)
            {
                case BusinessKind.Commercial:
                    return new[] { "ZoningCommercialHigh", "ZoningCommercialLow" }; // not shown: game sprite names
                case BusinessKind.Office:
                    return new[] { "ZoningOffice" }; // not shown: a game sprite name
                case BusinessKind.ItCluster:
                    return new[] { "IconPolicyHightech", "ZoningOffice" }; // not shown: game sprite names
                case BusinessKind.Leisure:
                    return new[] { "IconPolicyLeisure", "ZoningCommercialHigh" }; // not shown: game sprite names
                case BusinessKind.Tourism:
                    return new[] { "IconPolicyTourist", "ZoningCommercialHigh" }; // not shown: game sprite names
                case BusinessKind.Organic:
                    return new[] { "IconPolicyOrganic", "ZoningCommercialHigh" }; // not shown: game sprite names
                case BusinessKind.Farming:
                    return new[] { "IconPolicyFarming", "ZoningIndustrial" }; // not shown: game sprite names
                case BusinessKind.Forestry:
                    return new[] { "IconPolicyForest", "ZoningIndustrial" }; // not shown: game sprite names
                case BusinessKind.Oil:
                    return new[] { "IconPolicyOil", "ZoningIndustrial" }; // not shown: game sprite names
                case BusinessKind.Ore:
                    return new[] { "IconPolicyOre", "ZoningIndustrial" }; // not shown: game sprite names
                default:
                    return new[] { "ZoningIndustrial" }; // not shown: a game sprite name
            }
        }

        /// <summary>Draws the kind's icon on the coin; false when the game has none of them.</summary>
        private static bool ShowGlyph(UISprite coin, BusinessKind kind)
        {
            UITextureAtlas game = UIView.GetAView().defaultAtlas;
            var names = new System.Collections.Generic.List<string>();
            foreach (string plain in GlyphsFor(kind))
            {
                // The game keeps its zone icons in button states: the building panel's own
                // title icon is the "Hovered" one.
                names.Add(plain);
                names.Add(plain + "Hovered"); // not shown: a game sprite name
                names.Add(plain + "Focused"); // not shown: a game sprite name
            }

            foreach (string name in names)
            {
                UITextureAtlas.SpriteInfo info = game[name];
                if (info == null)
                {
                    continue;
                }

                UISprite glyph = coin.Find<UISprite>(GlyphName);
                if (glyph == null)
                {
                    glyph = coin.AddUIComponent<UISprite>();
                    glyph.name = GlyphName;
                    glyph.isInteractive = false;
                }

                glyph.atlas = game;
                glyph.spriteName = name;
                glyph.isVisible = true;
                // Inside the coin's ring, keeping the icon's own proportions.
                float room = coin.width * 0.56f;
                Vector2 natural = info.pixelSize;
                float scale = room / Mathf.Max(1f, Mathf.Max(natural.x, natural.y));
                glyph.size = new Vector2(natural.x * scale, natural.y * scale);
                glyph.relativePosition = new Vector3(
                    (coin.width - glyph.width) / 2f,
                    (coin.height - glyph.height) / 2f - coin.height * 0.03f
                );
                return true;
            }

            ModLog.Info(
                "coin icons: the game has no icon for " + kind + "; its coin shows the symbol"
            );
            return false;
        }

        /// <summary>Postfix: a stock the game drew its fallback for gets the coin and its symbol.</summary>
        public static void After(InvestmentAI ai, UISprite sprite)
        {
            if (
                sprite == null
                || ai == null
                || ai.m_info == null
                || sprite.spriteName != GameFallback
            )
            {
                return;
            }

            sprite.atlas = Atlas();
            sprite.spriteName = SpriteName;
            int companyId;
            LocalCompany company = CompanyListing.IsCompany(ai, out companyId)
                ? CompanyDesk.CompanyOf(companyId)
                : null;
            if (company != null && ShowGlyph(sprite, company.Kind))
            {
                return;
            }

            string text = StockSymbols.For(ai.m_info);
            UILabel symbol = sprite.Find<UILabel>(SymbolName);
            if (symbol == null)
            {
                symbol = sprite.AddUIComponent<UILabel>();
                symbol.name = SymbolName;
                symbol.isInteractive = false;
                symbol.textColor = SymbolColour;
                symbol.textAlignment = UIHorizontalAlignment.Center;
                symbol.verticalAlignment = UIVerticalAlignment.Middle;
                symbol.autoSize = false;
            }

            symbol.isVisible = true;
            symbol.text = text;
            symbol.textScale = CoinIcon.SymbolScale(text, sprite.width);
            symbol.size = sprite.size;
            // The face sits a little above the middle: the coin's side shows below it.
            symbol.relativePosition = new Vector3(0f, -sprite.height * 0.03f);
        }

        /// <summary>
        /// Main thread, once the hook is in (a hot reload, a city loaded): the game set its
        /// cards' icons when it built them, so set them again for the cards that exist.
        /// </summary>
        public static void RefreshAll()
        {
            try
            {
                EconomyPanel economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                if (economy == null)
                {
                    return;
                }

                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                foreach (
                    UISprite sprite in economy.component.GetComponentsInChildren<UISprite>(true)
                )
                {
                    UIComponent card = sprite.name == "InvestmentIcon" ? sprite.parent : null;
                    if (card == null || !(card.objectUserData is byte))
                    {
                        continue;
                    }

                    InvestmentInfo info = buffer[(byte)card.objectUserData].Info;
                    if (info != null && info.m_investmentAI != null)
                    {
                        info.m_investmentAI.SetSpriteName(sprite);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("coin icons: refresh failed: " + ex.Message);
            }
        }

        /// <summary>Hot reload: every coin goes back to the game's fallback icon.</summary>
        public static void RemoveAll()
        {
            try
            {
                foreach (UISprite sprite in UnityEngine.Object.FindObjectsOfType<UISprite>())
                {
                    if (sprite.atlas != null && sprite.atlas.name == AtlasName)
                    {
                        Before(sprite);
                        sprite.spriteName = GameFallback;
                        foreach (string child in new[] { SymbolName, GlyphName })
                        {
                            UIComponent c = sprite.Find(child);
                            if (c != null)
                            {
                                UnityEngine.Object.Destroy(c.gameObject);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("coin icons: remove failed: " + ex.Message);
            }
        }

        private static UITextureAtlas Atlas()
        {
            if (atlas != null)
            {
                return atlas;
            }

            byte[] mask = CoinIcon.Render(TextureSize);
            var pixels = new Color32[mask.Length];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    byte m = mask[y * TextureSize + x];
                    pixels[(TextureSize - 1 - y) * TextureSize + x] =
                        m == CoinIcon.Face ? FaceColour
                        : m == CoinIcon.Rim ? RimColour
                        : m == CoinIcon.Ring ? RingColour
                        : m == CoinIcon.Edge ? EdgeColour
                        : FoTheme.Clear;
                }
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.ARGB32, false);
            texture.name = SpriteName;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();

            UITextureAtlas game = UIView.GetAView().defaultAtlas;
            atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            atlas.name = AtlasName;
            atlas.material = UnityEngine.Object.Instantiate(game.material);
            atlas.material.mainTexture = texture;
            atlas.AddSprite(
                new UITextureAtlas.SpriteInfo
                {
                    name = SpriteName,
                    texture = texture,
                    region = new Rect(0f, 0f, 1f, 1f),
                }
            );
            return atlas;
        }
    }
}
