using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class SymbolBookTests
    {
        private static KeyValuePair<string, string> S(string prefab, string display) =>
            new KeyValuePair<string, string>(prefab, display);

        [Fact]
        public void FixedSymbolsAreUnique()
        {
            var dupes = SymbolBook
                .Fixed.Values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            Assert.Empty(dupes);
        }

        // The game's 13 stock prefabs (dev `log-symbols` dump, Financial Districts).
        private static readonly string[] Vanilla =
        {
            "Chirp Coin",
            "AirplaneInvestment",
            "ShipInvestment",
            "Death Care Sector Investment",
            "Health Care Sector Investment",
            "Fuel Company Investment",
            "Farming Industry Investment",
            "Forestry Industry Investment",
            "Generic Industry Investment",
            "Oil Industry Investment",
            "Ore Industry Investment",
            "TrafficJelly",
            "VeryLegitCompany",
        };

        [Fact]
        public void EveryGameStockHasAFixedSymbol()
        {
            Assert.All(Vanilla, p => Assert.True(SymbolBook.Fixed.ContainsKey(p), p));
        }

        [Fact]
        public void ChirperCryptoAndChirpyCruisesNoLongerCollide()
        {
            var map = SymbolBook.Assign(
                new[] { S("Chirp Coin", "ChirperCrypto"), S("ShipInvestment", "Chirpy Cruises") }
            );
            Assert.Equal("CHC", map["Chirp Coin"]);
            Assert.Equal("CCr", map["ShipInvestment"]);
        }

        [Fact]
        public void TheWholeGameListIsClashFree()
        {
            var all = Vanilla.Select(p => S(p, p)).Concat(new[] { S("Index", "Index") });
            Assert.Empty(SymbolBook.Clashes(SymbolBook.Assign(all)));
        }

        [Fact]
        public void KeyedByPrefabNotDisplayName()
        {
            // The same stock in another language keeps its symbol.
            var en = SymbolBook.Assign(new[] { S("Oil Industry Investment", "Oil Industry") });
            var de = SymbolBook.Assign(new[] { S("Oil Industry Investment", "Ölindustrie") });
            Assert.Equal("Oil", en["Oil Industry Investment"]);
            Assert.Equal("Oil", de["Oil Industry Investment"]);
            Assert.Equal("IDX", SymbolBook.Assign(new[] { S("Index", "Index") })["Index"]);
        }

        [Fact]
        public void UnknownStocksNeverTakeAReservedSymbol()
        {
            // "Oil" is reserved for the oil prefab even when it isn't listed.
            var map = SymbolBook.Assign(new[] { S("SomeModOil", "Oil") });
            Assert.NotEqual("Oil", map["SomeModOil"]);
            Assert.StartsWith("Oil", map["SomeModOil"]);
        }

        [Fact]
        public void ClashingUnknownStocksGetDistinctSymbols()
        {
            var map = SymbolBook.Assign(
                new[]
                {
                    S("ModA", "Chirper Crypto"),
                    S("ModB", "Chirper Crypto"),
                    S("ModC", "Chirper Crypto"),
                }
            );
            Assert.Equal(3, map.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Empty(SymbolBook.Clashes(map));
        }

        [Fact]
        public void AssignmentIsTheSameInAnyOrder()
        {
            var a = new[]
            {
                S("ModZ", "Zeta Works"),
                S("ModA", "Zeta Works"),
                S("TrafficJelly", "x"),
            };
            var first = SymbolBook.Assign(a);
            var second = SymbolBook.Assign(a.Reverse());
            foreach (var kv in first)
            {
                Assert.Equal(kv.Value, second[kv.Key]);
            }

            Assert.Equal("ZW", first["ModA"]); // ModA sorts first, so it gets the plain candidate
        }

        [Fact]
        public void ManyLookalikesStayUniqueWithDigitsLast()
        {
            var many = Enumerable
                .Range(0, 40)
                .Select(i => S("Mod" + i.ToString("00"), "Ab"))
                .ToList();
            var map = SymbolBook.Assign(many);
            Assert.Equal(40, map.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(map.Values, v => Assert.InRange(v.Length, 1, 6));
        }

        [Fact]
        public void BadInputIsSkipped()
        {
            var map = SymbolBook.Assign(
                new[] { S(null, "x"), S("", "y"), S("Q", null), S("Q", "dup") }
            );
            Assert.Single(map);
            Assert.Empty(SymbolBook.Assign(null));
        }

        [Fact]
        public void ClashesReportsSharedSymbols()
        {
            var map = new Dictionary<string, string>
            {
                { "a", "CCr" },
                { "b", "ccr" },
                { "c", "X" },
            };
            Assert.Single(SymbolBook.Clashes(map));
        }
    }
}
