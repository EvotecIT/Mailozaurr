using Mailozaurr.PowerShell;

namespace Mailozaurr.Tests;

public sealed class PowerShellDmarcCmdletTests {
    [Fact]
    public void ResolveTotalLimit_ExpandsAnUnboundTotalForALargerPerItemLimit() {
        long effective = CmdletGetDmarcReport.ResolveTotalLimit(
            configuredTotal: 100,
            perItem: 150,
            totalWasExplicitlyBound: false);

        Assert.Equal(150, effective);
    }

    [Fact]
    public void ResolveTotalLimit_PreservesAnExplicitTotalForPolicyValidation() {
        long effective = CmdletGetDmarcReport.ResolveTotalLimit(
            configuredTotal: 100,
            perItem: 150,
            totalWasExplicitlyBound: true);

        Assert.Equal(100, effective);
    }
}
