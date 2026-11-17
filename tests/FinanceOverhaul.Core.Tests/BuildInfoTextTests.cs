using System;
using System.Globalization;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class BuildInfoTextTests
    {
        [Fact]
        public void AssemblyIdentityComesFromThePackageVersion()
        {
            // Both projects take Major.Minor from package.json and let the build fill in
            // build and revision, so Cities sees a new identity on every build (live reload).
            System.Reflection.Assembly library = typeof(BuildInfoText).Assembly;
            Version identity = library.GetName().Version;
            Assert.Equal(4, identity.ToString().Split('.').Length);
            Assert.True(identity.Build > 0, "build number is generated per build");

            object[] info = library.GetCustomAttributes(
                typeof(System.Reflection.AssemblyInformationalVersionAttribute),
                false
            );
            string product = (
                (System.Reflection.AssemblyInformationalVersionAttribute)info[0]
            ).InformationalVersion;
            string[] parts = product.Split('+')[0].Split('.');
            Assert.True(parts.Length >= 3, "product version is semver: " + product);
            Assert.Equal(identity.Major, int.Parse(parts[0], CultureInfo.InvariantCulture));
            Assert.Equal(identity.Minor, int.Parse(parts[1], CultureInfo.InvariantCulture));
        }

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
