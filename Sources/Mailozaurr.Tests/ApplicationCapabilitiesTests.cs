using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationCapabilitiesTests {
    [Theory]
    [InlineData(MailProfileKind.Imap, MailCapability.ListFolders | MailCapability.SearchMessages | MailCapability.ReadMessages | MailCapability.MoveMessages | MailCapability.WaitForMessages)]
    [InlineData(MailProfileKind.Pop3, MailCapability.SearchMessages | MailCapability.ReadMessages | MailCapability.DeleteMessages)]
    [InlineData(MailProfileKind.Graph, MailCapability.ListFolders | MailCapability.SendMessages | MailCapability.ManageRules | MailCapability.ManageEvents | MailCapability.ManagePermissions)]
    [InlineData(MailProfileKind.Gmail, MailCapability.SearchMessages | MailCapability.MarkMessages | MailCapability.MoveMessages | MailCapability.SendMessages | MailCapability.UseThreads | MailCapability.UseLabels)]
    [InlineData(MailProfileKind.Smtp, MailCapability.SendMessages)]
    [InlineData(MailProfileKind.SendGrid, MailCapability.SendMessages)]
    public void CatalogExposesExpectedCapabilities(MailProfileKind kind, MailCapability required) {
        var capabilities = MailCapabilityCatalog.For(kind);

        Assert.Equal(kind, capabilities.Kind);
        Assert.True(capabilities.Supports(required));
    }

    [Fact]
    public void ProfileFallsBackToDefaultCapabilitiesWhenOverrideIsMissing() {
        var profile = new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap
        };

        var capabilities = profile.GetCapabilities();

        Assert.True(capabilities.Supports(MailCapability.SearchMessages));
        Assert.True(capabilities.Supports(MailCapability.WaitForMessages));
        Assert.False(capabilities.Supports(MailCapability.SendMessages));
    }

    [Fact]
    public void OperationResultHelpersProduceExpectedStatus() {
        var success = OperationResult.Success("Queued");
        var failure = OperationResult.Failure("auth_failed", "Authentication failed.");

        Assert.True(success.Succeeded);
        Assert.Equal("Queued", success.Message);
        Assert.False(failure.Succeeded);
        Assert.Equal("auth_failed", failure.Code);
    }
}
