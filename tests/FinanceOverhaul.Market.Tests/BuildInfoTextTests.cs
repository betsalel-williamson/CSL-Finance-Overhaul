using System;
using System.Globalization;
using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class BuildInfoTextTests
    {
        [Fact]
        public void FormatsEveryLineForBugReports()
        {
            string text = BuildInfoText.Format(
                "0.1.0",
                "0.1.9814.1044",
                "2026-11-14T08:34:47Z",
                true
            );
            string[] lines = text.Split('\n');
            Assert.Equal("Finance Overhaul 0.1.0", lines[0]);
            Assert.Equal("Assembly: 0.1.9814.1044", lines[1]);
            Assert.Equal("Built (UTC): 2026-11-14T08:34:47Z", lines[2]);
            Assert.StartsWith("Built (local): ", lines[3]);
            Assert.Equal("Build: dev", lines[4]);
        }

        [Fact]
        public void LocalTimeMatchesTheMachineZoneWithOffset()
        {
            DateTime utc = new DateTime(2026, 11, 14, 8, 34, 47, DateTimeKind.Utc);
            string expected = utc.ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
            Assert.Equal(expected, BuildInfoText.LocalTime("2026-11-14T08:34:47Z"));
        }

        [Fact]
        public void MissingPiecesAreMarkedNotDropped()
        {
            string text = BuildInfoText.Format(null, null, "not a date", false);
            Assert.Contains("Finance Overhaul ?", text);
            Assert.Contains("Assembly: ?", text);
            Assert.Contains("Built (UTC): ?", text);
            Assert.DoesNotContain("Built (local)", text);
            Assert.Contains("Build: release", text);
        }
    }
}
