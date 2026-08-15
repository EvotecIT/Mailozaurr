#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    [Fact]
    public async Task QueueListUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "list", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"queued-1\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueListCompactUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "list", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"MessageId\": \"queued-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"QueuedAt\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueGetUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "get", "--message-id", "queued-1", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("queued-1", fixture.QueueService.LastGetMessageId);
    }

    [Fact]
    public async Task QueueGetCompactUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "get", "--message-id", "queued-1", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"MessageId\": \"queued-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"QueuedAt\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueProcessUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "process", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fixture.QueueService.ProcessCalls);
    }

    [Fact]
    public async Task QueueDeadLetterListUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "dead-letter-list", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"MessageId\": \"dead-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"IsDeadLetter\": true", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftSaveUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "draft", "save",
                "--draft", "draft-1",
                "--name", "Quarterly report",
                "--profile", "work-imap",
                "--to", "alice@example.com",
                "--subject", "Quarterly report",
                "--text", "Body",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.DraftService.LastSavedDraft);
        Assert.Equal("draft-1", fixture.DraftService.LastSavedDraft!.Id);
        Assert.Equal("work-imap", fixture.DraftService.LastSavedDraft.Message.ProfileId);
    }

    [Fact]
    public async Task DraftListCompactUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "draft", "list", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Id\": \"draft-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"Message\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftGetCompactUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "draft", "get", "--draft", "draft-1", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Id\": \"draft-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"Message\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftSaveFromFileUsesDraftExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var importPath = CreateTemporaryFilePath("import-draft.json");
        await File.WriteAllTextAsync(importPath, JsonSerializer.Serialize(new MailDraft {
            Id = "imported-draft",
            Name = "Imported draft",
            Message = new DraftMessage {
                ProfileId = "work-imap",
                Subject = "Imported subject",
                To = {
                    new MessageRecipient { Address = "imported@example.com" }
                }
            }
        }));

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "draft", "save",
                "--file", importPath,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(importPath, fixture.DraftExchangeService.LastLoadedPath);
        Assert.NotNull(fixture.DraftService.LastSavedDraft);
        Assert.Equal("imported-draft", fixture.DraftService.LastSavedDraft!.Id);
    }

    [Fact]
    public async Task DraftExportUsesDraftExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var exportPath = CreateTemporaryFilePath("export-draft.json");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "draft", "export",
                "--draft", "draft-1",
                "--path", exportPath,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("draft-1", fixture.DraftService.LastRequestedDraftId);
        Assert.Equal(exportPath, fixture.DraftExchangeService.LastSavedPath);
    }

    [Fact]
    public async Task SendUsingDraftUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "send",
                "--draft", "draft-1",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("draft-1", fixture.DraftService.LastRequestedDraftId);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("work-imap", fixture.SendService.LastRequest!.ProfileId);
        Assert.False(fixture.SendService.LastRequest.QueueOnFailure);
        Assert.Equal("saved@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
    }

    [Fact]
    public async Task SendUsingFileUsesDraftExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var importPath = CreateTemporaryFilePath("send-draft.json");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "send",
                "--file", importPath,
                "--queue-on-failure",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(importPath, fixture.DraftExchangeService.LastLoadedPath);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("work-imap", fixture.SendService.LastRequest!.ProfileId);
        Assert.True(fixture.SendService.LastRequest.QueueOnFailure);
        Assert.Equal("imported@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("Imported subject", fixture.SendService.LastRequest.Message.Subject);
    }

    [Fact]
    public async Task SendUsesApplicationSendService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "send",
                "--profile", "work-imap",
                "--from", "sender@example.com",
                "--to", "alice@example.com",
                "--to", "bob@example.com",
                "--cc", "carol@example.com",
                "--reply-to", "reply@example.com",
                "--subject", "Quarterly report",
                "--text", "Plain text body",
                "--html", "<b>HTML body</b>",
                "--header", "X-Test=value",
                "--attachment", "C:\\Temp\\report.pdf",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("work-imap", fixture.SendService.LastRequest!.ProfileId);
        Assert.False(fixture.SendService.LastRequest.QueueOnFailure);
        Assert.Equal("sender@example.com", fixture.SendService.LastRequest.Message.From!.Address);
        Assert.Equal(2, fixture.SendService.LastRequest.Message.To.Count);
        Assert.Equal("alice@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("carol@example.com", fixture.SendService.LastRequest.Message.Cc[0].Address);
        Assert.Equal("reply@example.com", fixture.SendService.LastRequest.Message.ReplyTo[0].Address);
        Assert.Equal("Quarterly report", fixture.SendService.LastRequest.Message.Subject);
        Assert.Equal("Plain text body", fixture.SendService.LastRequest.Message.TextBody);
        Assert.Equal("<b>HTML body</b>", fixture.SendService.LastRequest.Message.HtmlBody);
        Assert.Equal("value", fixture.SendService.LastRequest.Message.Headers["X-Test"]);
        Assert.Equal("C:\\Temp\\report.pdf", fixture.SendService.LastRequest.Message.Attachments[0].Path);
    }
}
#endif
