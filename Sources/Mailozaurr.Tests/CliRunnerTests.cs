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
        Assert.Contains("Unknown option '--value'", stderr.ToString(), StringComparison.Ordinal);
    }








}
#endif
