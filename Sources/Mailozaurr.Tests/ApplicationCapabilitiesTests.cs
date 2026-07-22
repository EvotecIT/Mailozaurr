using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationCapabilitiesTests {
    [Theory]
    [InlineData(MailProfileKind.Imap, MailCapability.ListFolders | MailCapability.SearchMessages | MailCapability.ReadMessages | MailCapability.MoveMessages)]
    [InlineData(MailProfileKind.Graph, MailCapability.ListFolders | MailCapability.SendMessages | MailCapability.MarkMessages)]
    [InlineData(MailProfileKind.Gmail, MailCapability.SearchMessages | MailCapability.MarkMessages | MailCapability.MoveMessages | MailCapability.SendMessages)]
    [InlineData(MailProfileKind.Smtp, MailCapability.SendMessages)]
    [InlineData(MailProfileKind.SendGrid, MailCapability.SendMessages)]
    [InlineData(MailProfileKind.Mailgun, MailCapability.SendMessages)]
    [InlineData(MailProfileKind.Ses, MailCapability.SendMessages)]
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
        Assert.False(capabilities.Supports(MailCapability.WaitForMessages));
        Assert.False(capabilities.Supports(MailCapability.SendMessages));
    }

    [Theory]
    [InlineData(MailProfileKind.Pop3)]
    public void CatalogDoesNotAdvertiseProvidersWithoutNormalizedHandlers(MailProfileKind kind) {
        Assert.Equal(MailCapability.None, MailCapabilityCatalog.For(kind).Capabilities);
    }

    [Fact]
    public async Task BuiltApplicationReportsOnlyCapabilitiesBackedByRegisteredHandlers() {
        string directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var store = new FileMailProfileStore(Path.Combine(directory, "profiles.json"));
            await store.SaveAsync(new MailProfile {
                Id = "graph-actions-only",
                DisplayName = "Graph actions only",
                Kind = MailProfileKind.Graph
            });
            var application = new MailApplicationBuilder(new MailApplicationOptions {
                EnableGraphReadHandler = false,
                EnableGraphSendHandler = false
            }).UseProfileStore(store).Build();

            ProfileCapabilities? capabilities =
                await application.Profiles.GetCapabilitiesAsync("graph-actions-only");
            Assert.NotNull(capabilities);

            Assert.True(capabilities!.Supports(MailCapability.MarkMessages));
            Assert.True(capabilities.Supports(MailCapability.MoveMessages));
            Assert.False(capabilities.Supports(MailCapability.SearchMessages));
            Assert.False(capabilities.Supports(MailCapability.SendMessages));
        } finally {
            Directory.Delete(directory, true);
        }
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

    [Theory]
    [InlineData(MailProfileKind.SendGrid)]
    [InlineData(MailProfileKind.Mailgun)]
    [InlineData(MailProfileKind.Ses)]
    public void ProviderProfileAllowsSenderToBeSuppliedPerMessage(MailProfileKind kind) {
        var result = MailProfileValidator.Validate(new MailProfile {
            Id = kind.ToString().ToLowerInvariant(),
            DisplayName = kind.ToString(),
            Kind = kind
        });

        Assert.True(result.Succeeded);
        Assert.Contains(result.Warnings, warning => warning.Contains("DefaultSender", StringComparison.Ordinal));
    }
}
