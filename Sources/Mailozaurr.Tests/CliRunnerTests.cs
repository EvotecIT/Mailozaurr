#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr.Hosting;
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
    public async Task AtPrefixedOptionValueRemainsLiteral() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        string? profilesDirectory = null;

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--profiles-dir", "@profiles" },
            stdout,
            stderr,
            options => {
                profilesDirectory = options.ProfileStore.DirectoryPath;
                return fixture.CreateBuilder();
            });

        Assert.Equal(0, exitCode);
        Assert.Equal("@profiles", profilesDirectory);
    }

    [Fact]
    public async Task OptionNamesRemainCaseInsensitive() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        string? profilesDirectory = null;

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--PROFILES-DIR", "MixedCase", "--JSON" },
            stdout,
            stderr,
            options => {
                profilesDirectory = options.ProfileStore.DirectoryPath;
                return fixture.CreateBuilder();
            });

        Assert.Equal(0, exitCode);
        Assert.Equal("MixedCase", profilesDirectory);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    [Theory]
    [InlineData("[diagram]")]
    [InlineData("[suggest]")]
    public async Task ParserDirectiveCannotExecuteApplication(string directive) {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        bool builderInvoked = false;

        var exitCode = await CliRunner.RunAsync(
            new[] { directive, "profile", "list" },
            stdout,
            stderr,
            _ => {
                builderInvoked = true;
                return fixture.CreateBuilder();
            });

        Assert.Equal(1, exitCode);
        Assert.False(builderInvoked);
        Assert.False(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    [Fact]
    public async Task ScalarOptionCannotConsumeTrailingArgument() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        bool builderInvoked = false;
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "get",
                "--profile", "work-imap",
                "--message-id", "real-id",
                "stray",
                "--json"
            },
            stdout,
            stderr,
            _ => {
                builderInvoked = true;
                return fixture.CreateBuilder();
            });

        Assert.Equal(1, exitCode);
        Assert.False(builderInvoked);
        using JsonDocument document = JsonDocument.Parse(stderr.ToString());
        Assert.Contains("stray", document.RootElement.GetProperty("Error")
            .GetProperty("Message").GetString(), StringComparison.Ordinal);
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
    public async Task RepeatedOptionOccurrenceWithoutAValueIsRejected() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        bool builderInvoked = false;
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "get-many",
                "--profile", "work-imap",
                "--message-id", "id-1",
                "--message-id",
                "--json"
            },
            stdout,
            stderr,
            _ => {
                builderInvoked = true;
                return fixture.CreateBuilder();
            });

        Assert.Equal(1, exitCode);
        Assert.False(builderInvoked);
        using JsonDocument document = JsonDocument.Parse(stderr.ToString());
        Assert.Contains("requires a value", document.RootElement.GetProperty("Error")
            .GetProperty("Message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PresenceOnlyFlagRejectsAnExplicitBooleanValue() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        bool builderInvoked = false;
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "mark-read",
                "--profile", "work-imap",
                "--message-id", "id-1",
                "--unread", "false",
                "--json"
            },
            stdout,
            stderr,
            _ => {
                builderInvoked = true;
                return fixture.CreateBuilder();
            });

        Assert.Equal(1, exitCode);
        Assert.False(builderInvoked);
        using JsonDocument document = JsonDocument.Parse(stderr.ToString());
        Assert.Contains("Unrecognized command or argument", document.RootElement.GetProperty("Error")
            .GetProperty("Message").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("would-leak")]
    [InlineData("-p@ss")]
    [InlineData("--looks-like-an-option")]
    public async Task RawSecretCommandLineOptionIsRejectedWithoutExposingValue(string rawSecret) {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CliRunner.RunAsync(new[] {
            "profile", "set-secret", "--profile", "work", "--name", "password",
            "--value", rawSecret
        }, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unrecognized command or argument '--value'", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, stderr.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--value", "=")]
    [InlineData("--value", ":")]
    [InlineData("--VaLuE", ":")]
    public async Task InlineRawSecretCommandLineOptionIsRejectedWithoutExposingValue(
        string option,
        string delimiter) {
        const string rawSecret = "s3cr3t-value";
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CliRunner.RunAsync(new[] {
            "profile", "set-secret", "--profile", "work", "--name", "password",
            $"{option}{delimiter}{rawSecret}"
        }, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains($"{option}=<value>", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, stderr.ToString(), StringComparison.Ordinal);
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
