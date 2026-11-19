using System; // Math, Version
using System.Collections.Generic; // List
using FinanceOverhaul.Core; // DuplicateCopies, ModCopy
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class DuplicateCopiesTests
    {
        private static readonly ModCopy Workshop = new ModCopy
        {
            Name = "Finance Overhaul 0.1.0",
            Version = new Version(0, 1, 9815, 34926),
            Workshop = true,
            Path = "/workshop/3840721874",
        };

        private static readonly ModCopy Local = new ModCopy
        {
            Name = "Finance Overhaul 0.2.9816.30520",
            Version = new Version(0, 2, 9816, 30520),
            Workshop = false,
            Path = "/Mods/FinanceOverhaul",
        };

        [Fact]
        public void OneCopyIsNoConflict()
        {
            Assert.False(DuplicateCopies.Conflict(new List<ModCopy> { Local }));
            Assert.False(DuplicateCopies.Conflict(new List<ModCopy>()));
            Assert.False(DuplicateCopies.Conflict(null));
            Assert.Empty(DuplicateCopies.ToSwitchOff(new List<ModCopy> { Local }, true));
        }

        [Fact]
        public void TheNewestCopyIsSuggested()
        {
            var both = new List<ModCopy> { Workshop, Local };
            Assert.True(DuplicateCopies.Conflict(both));
            Assert.Same(Local, DuplicateCopies.Suggested(both));
            Assert.Same(Local, DuplicateCopies.Suggested(new List<ModCopy> { Local, Workshop }));
        }

        [Fact]
        public void OnATieTheLocalCopyIsSuggested()
        {
            var sameBuild = new ModCopy
            {
                Name = Workshop.Name,
                Version = Workshop.Version,
                Workshop = false,
                Path = "/Mods/FinanceOverhaul",
            };
            Assert.Same(
                sameBuild,
                DuplicateCopies.Suggested(new List<ModCopy> { Workshop, sameBuild })
            );
        }

        [Fact]
        public void YesKeepsTheSuggestedCopyAndNoKeepsTheOther()
        {
            var both = new List<ModCopy> { Workshop, Local };
            Assert.Equal(new[] { Workshop }, DuplicateCopies.ToSwitchOff(both, true).ToArray());
            Assert.Equal(new[] { Local }, DuplicateCopies.ToSwitchOff(both, false).ToArray());
        }

        [Fact]
        public void WithThreeCopiesNoChangesNothing()
        {
            var third = new ModCopy
            {
                Name = "Finance Overhaul 0.1.0",
                Version = new Version(0, 1, 9000, 1),
                Path = "/Mods/FinanceOverhaul copy",
            };
            var all = new List<ModCopy> { Workshop, Local, third };
            Assert.Equal(2, DuplicateCopies.ToSwitchOff(all, true).Count);
            Assert.DoesNotContain(Local, DuplicateCopies.ToSwitchOff(all, true));
            Assert.Empty(DuplicateCopies.ToSwitchOff(all, false));
        }

        [Fact]
        public void TheMessageSaysWhatEachAnswerKeeps()
        {
            string message = DuplicateCopies.Message(new List<ModCopy> { Workshop, Local });
            Assert.Equal(
                "Two copies are switched on and both would act on your city.\n\n"
                    + "Yes: keep 0.2.9816.30520 (local)\nNo: keep 0.1.0 (Workshop)",
                message
            );
            Assert.Equal("Finance Overhaul is switched on twice", DuplicateCopies.Title());
        }

        [Fact]
        public void TheMessageFitsTheGamesDialog()
        {
            // The game's Yes / No dialog shows about five lines of some 45 characters.
            string message = DuplicateCopies.Message(new List<ModCopy> { Workshop, Local });
            int lines = 0;
            foreach (string line in message.Split('\n'))
            {
                lines += Math.Max(1, (line.Length + 44) / 45);
            }

            Assert.True(lines <= 5, "the message takes " + lines + " lines");
        }

        [Fact]
        public void WithMoreThanTwoCopiesTheMessageCountsThem()
        {
            var third = new ModCopy { Name = "Finance Overhaul 0.1.0", Path = "/Mods/copy" };
            string message = DuplicateCopies.Message(new List<ModCopy> { Workshop, Local, third });
            Assert.StartsWith("3 copies are switched on", message);
            Assert.Contains("Yes: keep only 0.2.9816.30520 (local)", message);
        }

        [Fact]
        public void ACopyWithoutANameStillReads()
        {
            Assert.Equal(
                "unknown version (local)",
                DuplicateCopies.Describe(new ModCopy { Path = "/x" })
            );
            Assert.Equal(
                "unknown version (Workshop)",
                DuplicateCopies.Describe(new ModCopy { Name = "Finance Overhaul", Workshop = true })
            );
        }
    }
}
