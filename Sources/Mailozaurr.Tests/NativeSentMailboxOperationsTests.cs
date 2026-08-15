using Xunit;

namespace Mailozaurr.Tests;

public sealed class NativeSentMailboxOperationsTests {
    [Fact]
    public void ResolveGraphSentFolderName_PrefersRequestedFolder() {
        var folder = GraphNativeMailboxOperations.ResolveSentFolderName(
            requestedSentFolder: " Team/Sent ",
            configuredSentFolder: "ConfigSent");

        Assert.Equal("Team/Sent", folder);
    }

    [Fact]
    public void ResolveGraphSentFolderName_UsesConfiguredWhenRequestedMissing() {
        var folder = GraphNativeMailboxOperations.ResolveSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: " ConfigSent ");

        Assert.Equal("ConfigSent", folder);
    }

    [Fact]
    public void ResolveGraphSentFolderName_UsesFallbackWhenOverridesMissing() {
        var folder = GraphNativeMailboxOperations.ResolveSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: null);

        Assert.Equal(GraphNativeMailboxOperations.DefaultSentFolder, folder);
    }

    [Fact]
    public void ResolveGraphSentFolderSelector_UsesGraphFolderSelectorRules() {
        var selector = GraphNativeMailboxOperations.ResolveSentFolderSelector(
            requestedSentFolder: null,
            configuredSentFolder: "Sent Items");

        Assert.Equal(GraphMailboxBrowser.ResolveFolderSelector("Sent Items"), selector);
    }

    [Fact]
    public void ResolveGmailSentLabelId_IsAlwaysSystemSentLabel() {
        var label = GmailNativeMailboxOperations.ResolveSentLabelId(
            requestedSentFolder: "CustomSent",
            configuredSentFolder: "ConfiguredSent");

        Assert.Equal(GmailNativeMailboxOperations.DefaultSentLabelId, label);
    }

    [Fact]
    public void ResolveGmailSentFolderName_PrefersRequestedThenConfiguredThenFallback() {
        var requested = GmailNativeMailboxOperations.ResolveSentFolderName(
            requestedSentFolder: " Requested ",
            configuredSentFolder: "Configured");
        var configured = GmailNativeMailboxOperations.ResolveSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: " Configured ");
        var fallback = GmailNativeMailboxOperations.ResolveSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: null);

        Assert.Equal("Requested", requested);
        Assert.Equal("Configured", configured);
        Assert.Equal(GmailNativeMailboxOperations.DefaultSentLabelId, fallback);
    }
}