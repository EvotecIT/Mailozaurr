namespace Mailozaurr.Tests;

public sealed class MailDeliveryEngineTests {
    [Theory]
    [InlineData(451, false)]
    [InlineData(550, false)]
    [InlineData(250, true)]
    public async Task JsonRouteFallsBackAfterDefiniteRejectionAndStopsAfterLostAcknowledgement(int code, bool dropAfterData) {
        await using var primary = new LoopbackSmtpServer(contentCode: code, dropAfterData: dropAfterData);
        await using var fallback = new LoopbackSmtpServer();
        var configuration = MailDeliveryConfiguration.ParseJson($$"""
            {
              "profiles": {
                "primary": { "from": "sender@example.test", "smtp": { "server": "127.0.0.1", "port": {{primary.Port}}, "security": "None" } },
                "fallback": { "from": "sender@example.test", "smtp": { "server": "127.0.0.1", "port": {{fallback.Port}}, "security": "None" } }
              },
              "routes": { "reports": ["primary", "fallback"] }
            }
            """);
        var engine = new MailDeliveryEngine(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var result = await engine.SendAsync("reports", Message(), timeout.Token);

        Assert.Equal(1, primary.ContentSubmissions);
        if (dropAfterData) {
            Assert.False(result.Accepted);
            Assert.Equal(MailDeliveryOutcome.Unconfirmed, Assert.Single(result.Attempts).Outcome);
            Assert.Equal(0, fallback.Connections);
        } else {
            Assert.True(result.Accepted);
            Assert.Equal(new[] { MailDeliveryOutcome.SafeToFallback, MailDeliveryOutcome.Accepted }, result.Attempts.Select(attempt => attempt.Outcome));
            Assert.Equal(1, fallback.Accepted);
        }
    }

    [Fact]
    public async Task TypedRouteRereadsAReplacedCredentialForEachSendAndCanUseSmtpWhenGraphCredentialIsMissing() {
        await using var smtp = new LoopbackSmtpServer();
        string path = Path.Combine(Path.GetTempPath(), "mailozaurr-route-" + Guid.NewGuid().ToString("N"));
        var credentials = new ObservedMissingCredentialResolver();
        var engine = new MailDeliveryEngine(new MailDeliveryConfiguration {
            Profiles = {
                ["graph"] = new() {
                    From = "sender@example.test",
                    Graph = new() {
                        TenantId = Guid.NewGuid(), ClientId = Guid.NewGuid(),
                        ClientSecret = new MailCredentialReference("file", path)
                    }
                },
                ["smtp"] = new() {
                    From = "sender@example.test",
                    Smtp = new() { Server = "127.0.0.1", Port = smtp.Port, Security = SmtpSecurityMode.None }
                }
            },
            Routes = { ["reports"] = new() { "graph", "smtp" } }
        }, credentials);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try {
            File.WriteAllText(path, "first\n");
            var first = await engine.SendAsync("reports", Message(), timeout.Token);
            File.WriteAllText(path + ".next", "second\r\n");
            File.Replace(path + ".next", path, null);
            var second = await engine.SendAsync("reports", Message(), timeout.Token);

            Assert.Equal(new[] { "first", "second" }, credentials.Seen);
            foreach (var result in new[] { first, second }) {
                Assert.True(result.Accepted);
                Assert.Equal("credential_unavailable", result.Attempts[0].Code);
                Assert.Equal(MailProfileKind.Graph, result.Attempts[0].Kind);
                Assert.Equal(MailProfileKind.Smtp, result.Attempts[1].Kind);
            }
            Assert.Equal(2, smtp.Accepted);
        } finally {
            File.Delete(path);
            File.Delete(path + ".next");
        }
    }

    [Fact]
    public void ShareableConfigurationRejectsAnInlineCredentialValue() {
        const string json = """
            { "profiles": { "smtp": { "from": "sender@example.test", "smtp": { "password": "synthetic-password" } } } }
            """;
        Assert.Throws<System.Text.Json.JsonException>(() => MailDeliveryConfiguration.ParseJson(json));
    }

    [Theory]
    [InlineData("Sender")]
    [InlineData("resent-to")]
    public async Task RouteRejectsCustomEnvelopeHeadersBeforeConnecting(string header) {
        await using var smtp = new LoopbackSmtpServer();
        var engine = new MailDeliveryEngine(new MailDeliveryConfiguration {
            Profiles = {
                ["smtp"] = new() {
                    From = "sender@example.test",
                    Smtp = new() { Server = "127.0.0.1", Port = smtp.Port, Security = SmtpSecurityMode.None }
                }
            },
            Routes = { ["reports"] = new() { "smtp" } }
        });
        DraftMessage draft = Message();
        draft.Headers[header] = "unapproved@example.test";
        await Assert.ThrowsAsync<ArgumentException>(() => engine.SendAsync("reports", draft));
        Assert.Equal(0, smtp.Connections);
    }

    private static DraftMessage Message() => new() {
        Subject = "Synthetic report", TextBody = "Report", To = new() { new() { Address = "recipient@example.test" } }
    };

    private sealed class ObservedMissingCredentialResolver : IMailCredentialResolver {
        private readonly MailCredentialResolver _files = new();
        public List<string?> Seen { get; } = new();

        public async Task<string?> ResolveAsync(MailCredentialReference reference, CancellationToken cancellationToken = default) {
            Seen.Add(await _files.ResolveAsync(reference, cancellationToken));
            // The test observes rotation without using an identity service or a real Graph credential.
            return null;
        }
    }
}
