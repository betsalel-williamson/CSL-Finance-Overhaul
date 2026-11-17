using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>Tests switch the language, so they must not run beside tests that read text.</summary>
    [CollectionDefinition("Strings language", DisableParallelization = true)]
    public class StringsLanguageCollection { }

    [Collection("Strings language")]
    public class StringsTests : IDisposable
    {
        public void Dispose()
        {
            Strings.Language = Strings.DefaultLanguage;
            Strings.RemoveLanguage("xx");
        }

        [Fact]
        public void EnglishIsTheDefaultAndAnUnknownIdShowsItself()
        {
            Assert.Equal("en", Strings.Language);
            Assert.Equal("Stock Monitor", Strings.Get("monitor.title"));
            Assert.Equal("no.such.id", Strings.Get("no.such.id"));
        }

        [Fact]
        public void FormatFillsPlaceholders()
        {
            Assert.Equal("Orders (6)", Strings.Format("monitor.filter.ordersCount", "6"));
            Assert.Equal("₡42 away", Strings.Format("orders.away", "42"));
        }

        [Fact]
        public void ALanguageFallsBackToEnglishIdById()
        {
            Strings.AddLanguage(
                "xx",
                new Dictionary<string, string>
                {
                    { "monitor.filter.all", "Alle" },
                    { "orders.away", "noch ₡{0}" },
                }
            );
            Strings.Language = "xx";
            Assert.Equal("Alle", Strings.Get("monitor.filter.all"));
            Assert.Equal("noch ₡42", Strings.Format("orders.away", "42"));
            Assert.Equal("Portfolio", Strings.Get("monitor.filter.portfolio")); // not translated
            Assert.Equal("Alle", StockMonitor.FilterLabel(MonitorFilter.All)); // the code follows
        }

        [Fact]
        public void ARegionalCodeIsCutToItsLanguageAndEmptyMeansEnglish()
        {
            Strings.Language = "pt-BR";
            Assert.Equal("pt", Strings.Language);
            Strings.Language = "";
            Assert.Equal("en", Strings.Language);
            Strings.Language = null;
            Assert.Equal("en", Strings.Language);
        }

        [Fact]
        public void ATranslationWithABadPlaceholderStillShowsSomething()
        {
            Strings.AddLanguage(
                "xx",
                new Dictionary<string, string> { { "orders.away", "₡{3} weg" } }
            );
            Strings.Language = "xx";
            Assert.Equal("₡{3} weg", Strings.Format("orders.away", "42"));
        }

        [Fact]
        public void PluralsFollowTheLanguagesRule()
        {
            Assert.Equal("1 share", Strings.Plural("shares", 1, "1"));
            Assert.Equal("2 shares", Strings.Plural("shares", 2, "2"));
            Assert.Equal("0 shares", Strings.Plural("shares", 0, "0"));

            Assert.Equal("one", Strings.PluralForm("fr", 0));
            Assert.Equal("other", Strings.PluralForm("fr", 2));
            Assert.Equal("one", Strings.PluralForm("ru", 21));
            Assert.Equal("few", Strings.PluralForm("ru", 3));
            Assert.Equal("many", Strings.PluralForm("ru", 11));
            Assert.Equal("many", Strings.PluralForm("ru", 5));
            Assert.Equal("few", Strings.PluralForm("pl", 22));
            Assert.Equal("many", Strings.PluralForm("pl", 21));
            Assert.Equal("other", Strings.PluralForm("zh", 1));
            Assert.Equal("other", Strings.PluralForm("ko", 1));
        }

        [Fact]
        public void ALanguageWithoutAFormUsesOther()
        {
            Strings.AddLanguage(
                "ru",
                new Dictionary<string, string>
                {
                    { "shares.one", "{0} акция" },
                    { "shares.few", "{0} акции" },
                    { "shares.many", "{0} акций" },
                }
            );
            try
            {
                Strings.Language = "ru";
                Assert.Equal("1 акция", Strings.Plural("shares", 1, "1"));
                Assert.Equal("3 акции", Strings.Plural("shares", 3, "3"));
                Assert.Equal("5 акций", Strings.Plural("shares", 5, "5"));
                // days left has no Russian entry: English, and English has no "few".
                Assert.Equal("3 days left", Strings.Plural("daysLeft", 3, "3"));
            }
            finally
            {
                Strings.RemoveLanguage("ru");
            }
        }

        [Fact]
        public void EveryEnglishEntryIsWellFormed()
        {
            var placeholder = new Regex(@"\{(\d+)\}");
            foreach (string id in Strings.Ids)
            {
                string text = Strings.Get(id);
                Assert.False(string.IsNullOrEmpty(text), id + " is empty");
                Assert.Equal(text.Trim(), text);
                // Placeholders are numbered from 0 with none skipped.
                var seen = new SortedDictionary<int, bool>();
                foreach (Match m in placeholder.Matches(text))
                {
                    seen[int.Parse(m.Groups[1].Value)] = true;
                }

                int expect = 0;
                foreach (int n in seen.Keys)
                {
                    Assert.True(n == expect, id + " skips placeholder {" + expect + "}");
                    expect++;
                }

                // A plural entry has its ".other" form to fall back to.
                if (id.EndsWith(".one", StringComparison.Ordinal))
                {
                    string other = id.Substring(0, id.Length - 4) + ".other";
                    Assert.Contains(other, Strings.Ids);
                }
            }
        }
    }
}
