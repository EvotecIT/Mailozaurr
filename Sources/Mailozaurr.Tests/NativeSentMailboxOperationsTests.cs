using Xunit;

namespace Mailozaurr.Tests;

public sealed class NativeSentMailboxOperationsTests {
    [Fact]
    public void ResolveGraphSentFolderName_PrefersRequestedFolder() {
        var folder = NativeSentMailboxOperations.ResolveGraphSentFolderName(
            requestedSentFolder: " Team/Sent ",
            configuredSentFolder: "ConfigSent");

        Assert.Equal("Team/Sent", folder);
    }

    [Fact]
    public void ResolveGraphSentFolderName_UsesConfiguredWhenRequestedMissing() {
        var folder = NativeSentMailboxOperations.ResolveGraphSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: " ConfigSent ");

        Assert.Equal("ConfigSent", folder);
    }

    [Fact]
    public void ResolveGraphSentFolderName_UsesFallbackWhenOverridesMissing() {
        var folder = NativeSentMailboxOperations.ResolveGraphSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: null);

        Assert.Equal(NativeSentMailboxOperations.DefaultGraphSentFolder, folder);
    }

    [Fact]
    public void ResolveGraphSentFolderSelector_UsesGraphFolderSelectorRules() {
        var selector = NativeSentMailboxOperations.ResolveGraphSentFolderSelector(
            requestedSentFolder: null,
            configuredSentFolder: "Sent Items");

        Assert.Equal(GraphMailboxBrowser.ResolveFolderSelector("Sent Items"), selector);
    }

    [Fact]
    public void ResolveGmailSentLabelId_IsAlwaysSystemSentLabel() {
        var label = NativeSentMailboxOperations.ResolveGmailSentLabelId(
            requestedSentFolder: "CustomSent",
            configuredSentFolder: "ConfiguredSent");

        Assert.Equal(NativeSentMailboxOperations.DefaultGmailSentLabelId, label);
    }

    [Fact]
    public void ResolveGmailSentFolderName_PrefersRequestedThenConfiguredThenFallback() {
        var requested = NativeSentMailboxOperations.ResolveGmailSentFolderName(
            requestedSentFolder: " Requested ",
            configuredSentFolder: "Configured");
        var configured = NativeSentMailboxOperations.ResolveGmailSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: " Configured ");
        var fallback = NativeSentMailboxOperations.ResolveGmailSentFolderName(
            requestedSentFolder: null,
            configuredSentFolder: null);

        Assert.Equal("Requested", requested);
        Assert.Equal("Configured", configured);
        Assert.Equal(NativeSentMailboxOperations.DefaultGmailSentLabelId, fallback);
    }
}
