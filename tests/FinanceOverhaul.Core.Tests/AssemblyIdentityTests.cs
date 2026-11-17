using System;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// Hot reload: Cities loads a fresh copy of every mod DLL, but Mono keeps resolving
    /// the first-loaded assembly with an identical identity. The Market library must get
    /// a new build/revision each compile (ADR 0002), like the host assembly.
    /// </summary>
    public class AssemblyIdentityTests
    {
        [Fact]
        public void MarketAssemblyVersion_ChangesPerBuild()
        {
            Version v = typeof(ShareAmountInput).Assembly.GetName().Version;
            Assert.True(
                v.Build > 0,
                "Market AssemblyVersion must use Major.Minor.* (got " + v + ")"
            );
        }
    }
}
