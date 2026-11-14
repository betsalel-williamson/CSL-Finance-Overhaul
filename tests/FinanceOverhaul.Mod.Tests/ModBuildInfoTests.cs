using System;
using System.Globalization;
using Xunit;
using ModType = FinanceOverhaul.Mod;

namespace FinanceOverhaul.Host.Tests
{
    /// <summary>
    /// Mod-surface identity tests (no Cities assemblies). In-game Harmony /
    /// Stock Exchange checks stay on the local game loop.
    /// </summary>
    public class ModBuildInfoTests
    {
        [Fact]
        public void GetProductVersionDisplay_MatchesPackageSemverShape()
        {
            string product = ModType.GetProductVersionDisplay();
            Assert.False(string.IsNullOrEmpty(product));
            string[] parts = product.Split('.');
            Assert.True(parts.Length >= 2);
            Assert.True(int.TryParse(parts[0], out _));
            Assert.True(int.TryParse(parts[1], out _));
        }

        [Fact]
        public void GetAssemblyIdentityDisplay_HasFourComponents()
        {
            string asm = ModType.GetAssemblyIdentityDisplay();
            Assert.False(string.IsNullOrEmpty(asm));
            Assert.Equal(4, asm.Split('.').Length);
        }

        [Fact]
        public void GetAssemblyBuildTimestampUtcDisplay_Parses()
        {
            string built = ModType.GetAssemblyBuildTimestampUtcDisplay();
            Assert.False(string.IsNullOrEmpty(built));
            Assert.True(
                DateTime.TryParse(
                    built,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out DateTime parsed
                )
            );
            Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        }
    }
}
