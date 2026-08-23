#if NET8_0_OR_GREATER
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    [Fact]
    public async Task ChangeFeedCommandDelegatesCursorAndLimit() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "changes", "--profile", "work-imap", "--cursor", "100", "--limit", "25", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.True(exitCode == 0, stderr.ToString());
        Assert.NotNull(fixture.ChangeFeedService.LastRequest);
        Assert.Equal("100", fixture.ChangeFeedService.LastRequest!.Cursor);
        Assert.Equal(25, fixture.ChangeFeedService.LastRequest.MaxChanges);
        Assert.Contains("durable-history", stdout.ToString(), StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    [Fact]
    public async Task ChangeSubscriptionCommandPreservesBlankFolderEvidence() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "subscribe-changes", "--profile", "gmail-work",
                "--folder", " ", "--topic", "projects/test/topics/mail"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.True(exitCode == 0, stderr.ToString());
        Assert.NotNull(fixture.ChangeFeedService.LastSubscriptionRequest);
        Assert.Equal(new[] { " " }, fixture.ChangeFeedService.LastSubscriptionRequest!.FolderIds);
    }
}
#endif
