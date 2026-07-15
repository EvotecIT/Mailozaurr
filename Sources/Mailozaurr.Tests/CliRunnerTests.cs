#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr.Application;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    [Fact]
    public async Task HelpIsShownWhenNoArgumentsAreProvided() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(Array.Empty<string>(), stdout, stderr, _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("Mailozaurr CLI", stdout.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("profile", "list", "--profiel")]
    [InlineData("profile", "list", "unexpected")]
    public async Task InvalidArgumentsAreRejectedBeforeApplicationExecution(params string[] args) {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CliRunner.RunAsync(args, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    [Fact]
    public async Task MissingOptionValueIsReportedAsJsonWhenRequested() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "show", "--profile", "--json" }, stdout, stderr);

        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stderr.ToString());
        Assert.Contains("requires a value", document.RootElement.GetProperty("Error")
            .GetProperty("Message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RawSecretCommandLineOptionIsRejected() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CliRunner.RunAsync(new[] {
            "profile", "set-secret", "--profile", "work", "--name", "password",
            "--value", "would-leak"
        }, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unrecognized command or argument '--value'", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("would-leak", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandScopedOptionCannotConsumeARootCommand() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        bool builderInvoked = false;
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "--profile", "send", "--json" },
            stdout,
            stderr,
            _ => {
                builderInvoked = true;
                return fixture.CreateBuilder();
            });

        Assert.Equal(1, exitCode);
        Assert.False(builderInvoked);
        using JsonDocument document = JsonDocument.Parse(stderr.ToString());
        Assert.Contains("--profile", document.RootElement.GetProperty("Error")
            .GetProperty("Message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionsAreValidatedAgainstTheSelectedCommand() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        bool builderInvoked = false;
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--subject", "ignored-before" },
            stdout,
            stderr,
            _ => {
                builderInvoked = true;
                return fixture.CreateBuilder();
            });

        Assert.Equal(1, exitCode);
        Assert.False(builderInvoked);
        Assert.Contains("Unrecognized command or argument '--subject'", stderr.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommandNameRemainsVisibleInTheParseError() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CliRunner.RunAsync(
            new[] { "definitely-not-a-command" },
            stdout,
            stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("definitely-not-a-command", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpIsGeneratedForTheSelectedTypedCommand() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "show", "--help" },
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("mailozaurr profile show", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("--profile <value> (required)", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("graph-bootstrap", stdout.ToString(), StringComparison.Ordinal);
    }







}
#endif
