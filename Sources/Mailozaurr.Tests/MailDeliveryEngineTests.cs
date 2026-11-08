namespace Mailozaurr.Tests;

public sealed class MailDeliveryEngineTests {
    [Theory]
    [InlineData(451, false)]
    [InlineData(550, false)]
    [InlineData(250, true)]
    public async Task FallbackRequiresKnownNonacceptanceEvenWithRetries(int code, bool drop) {
        await using var primary = new LoopbackSmtpServer(contentCode: code, dropAfterData: drop);
        await using var backup = new LoopbackSmtpServer();
        var configuration = Configuration(primary, backup);
        configuration.Routes["reports"].RetryCount = 10;
        using var engine = new MailDeliveryEngine(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await engine.SendAsync("reports", Message(), timeout.Token);
        Assert.Equal(1, primary.ContentSubmissions);
        if (drop) {
            Assert.False(result.Accepted);
            Assert.Equal(MailDeliveryOutcome.Unconfirmed, Assert.Single(result.Attempts).Outcome);
            Assert.Equal(0, backup.Connections);
        } else {
            Assert.True(result.Accepted);
            Assert.Equal(new[] { MailDeliveryOutcome.SafeToFallback, MailDeliveryOutcome.Accepted }, result.Attempts.Select(attempt => attempt.Outcome));
            Assert.Equal(1, backup.Accepted);
        }
    }

    [Fact]
    public async Task RetryCountBoundsAdditionalAttemptsAndPermanentRejectionIsNotRetried() {
        await using var transient = new LoopbackSmtpServer(contentCode: 451);
        await using var permanent = new LoopbackSmtpServer(contentCode: 550);
        var configuration = Configuration(transient, permanent);
        configuration.Routes["reports"].RetryCount = 2;
        configuration.Routes["reports"].RetryDelay = TimeSpan.Zero;
        using var engine = new MailDeliveryEngine(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await engine.SendAsync("reports", Message(), timeout.Token);
        Assert.False(result.Accepted);
        Assert.Equal(4, result.Attempts.Count);
        Assert.Equal(3, transient.ContentSubmissions);
        Assert.Equal(1, permanent.ContentSubmissions);
        Assert.True(result.Attempts[0].Retryable);
        Assert.False(result.Attempts[1].Retryable);
    }

    [Fact]
    public async Task TypedAndJsonConfigurationUseSameRoundRobinPolicyAndCallerSender() {
        await using var first = new LoopbackSmtpServer();
        await using var second = new LoopbackSmtpServer();
        var configuration = Configuration(first, second);
        configuration.Routes["reports"].Strategy = MailDeliveryStrategy.RoundRobin;
        string json = System.Text.Json.JsonSerializer.Serialize(configuration, MailDeliveryJsonContext.Default.MailDeliveryConfiguration);
        using var engine = new MailDeliveryEngine(MailDeliveryConfiguration.ParseJson(json));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        for (int i = 0; i < 4; i++) {
            var draft = Message();
            draft.From = new() { Address = "custom@example.test" };
            Assert.True((await engine.SendAsync("reports", draft, timeout.Token)).Accepted);
        }
        Assert.Equal(2, first.Accepted);
        Assert.Equal(2, second.Accepted);
        Assert.All(first.MailFromCommands, command => Assert.Contains("custom@example.test", command));
        Assert.All(second.RecipientCommands, command => Assert.Contains("recipient@example.test", command));
    }

    [Fact]
    public async Task SmtpProfileUsesCanonicalMultiServerSelectionAcrossMessages() {
        await using var first = new LoopbackSmtpServer();
        await using var second = new LoopbackSmtpServer();
        var configuration = Configuration(first);
        configuration.Profiles["p0"].Profile.SmtpDelivery = new() {
            Servers = new() { new() { Server = "127.0.0.1", Port = first.Port }, new() { Server = "127.0.0.1", Port = second.Port } },
            Strategy = SmtpServerStrategy.RoundRobin, MaxAttempts = 2
        };
        using var engine = new MailDeliveryEngine(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Assert.True((await engine.SendAsync("reports", Message(), timeout.Token)).Accepted);
        Assert.True((await engine.SendAsync("reports", Message(), timeout.Token)).Accepted);
        Assert.Equal(1, first.Accepted);
        Assert.Equal(1, second.Accepted);
    }

    [Theory]
    [InlineData(MailProfileKind.Graph, MailSecretNames.ClientSecret)]
    [InlineData(MailProfileKind.Gmail, MailSecretNames.AccessToken)]
    [InlineData(MailProfileKind.SendGrid, MailSecretNames.ApiKey)]
    [InlineData(MailProfileKind.Mailgun, MailSecretNames.ApiKey)]
    [InlineData(MailProfileKind.Ses, MailSecretNames.SecretAccessKey)]
    public async Task ProviderCredentialIsRereadAndMissingCredentialUsesBackup(MailProfileKind kind, string secretName) {
        await using var smtp = new LoopbackSmtpServer();
        string path = Path.Combine(Path.GetTempPath(), "mailozaurr-route-" + Guid.NewGuid().ToString("N"));
        var resolver = new ObservedMissingCredentialResolver();
        var configuration = Configuration(smtp);
        configuration.Profiles["primary"] = new() {
            Profile = new() { Kind = kind, DefaultSender = "sender@example.test", Settings = new() { ["domain"] = "example.test" } },
            Credentials = new() { [secretName] = new("file", path) }
        };
        if (kind == MailProfileKind.Graph) {
            configuration.Profiles["primary"].Profile.Settings["tenantId"] = Guid.NewGuid().ToString();
            configuration.Profiles["primary"].Profile.Settings["clientId"] = Guid.NewGuid().ToString();
        }
        if (kind == MailProfileKind.Ses)
            configuration.Profiles["primary"].Credentials[MailSecretNames.AccessKeyId] = new("file", path);
        configuration.Routes["reports"].Profiles.Insert(0, "primary");
        using var engine = new MailDeliveryEngine(configuration, resolver);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try {
            File.WriteAllText(path, "first\n");
            Assert.True((await engine.SendAsync("reports", Message(), timeout.Token)).Accepted);
            File.WriteAllText(path + ".next", "second\r\n");
            File.Replace(path + ".next", path, null);
            Assert.True((await engine.SendAsync("reports", Message(), timeout.Token)).Accepted);
            Assert.Equal(new[] { "first", "second" }, resolver.Seen);
            Assert.Equal(2, smtp.Accepted);
        } finally {
            File.Delete(path);
            File.Delete(path + ".next");
        }
    }

    [Theory]
    [InlineData("Sender")]
    [InlineData("resent-to")]
    public async Task CustomRoutingHeadersFailBeforeConnecting(string header) {
        await using var smtp = new LoopbackSmtpServer();
        using var engine = new MailDeliveryEngine(Configuration(smtp));
        var draft = Message();
        draft.Headers[header] = "other@example.test";
        await Assert.ThrowsAsync<ArgumentException>(() => engine.SendAsync("reports", draft));
        Assert.Equal(0, smtp.Connections);
    }

    [Fact]
    public async Task CoolingDownPrimaryStillAllowsBackupOnNextMessage() {
        await using var primary = new LoopbackSmtpServer(contentCode: 451);
        await using var backup = new LoopbackSmtpServer();
        var configuration = Configuration(primary, backup);
        configuration.Profiles["p0"].Profile.SmtpDelivery = new() {
            Servers = new() { new() { Server = "127.0.0.1", Port = primary.Port } },
            MaxAttempts = 1, CircuitBreakMinutes = 2
        };
        using var engine = new MailDeliveryEngine(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Assert.True((await engine.SendAsync("reports", Message(), timeout.Token)).Accepted);
        var second = await engine.SendAsync("reports", Message(), timeout.Token);
        Assert.True(second.Accepted);
        Assert.Equal(MailDeliveryOutcome.SafeToFallback, second.Attempts[0].Outcome);
        Assert.Equal(1, primary.ContentSubmissions);
        Assert.Equal(2, backup.Accepted);
    }

    [Theory]
    [InlineData(MailProfileKind.Graph)]
    [InlineData(MailProfileKind.Gmail)]
    [InlineData(MailProfileKind.SendGrid)]
    [InlineData(MailProfileKind.Mailgun)]
    [InlineData(MailProfileKind.Ses)]
    [InlineData(MailProfileKind.Smtp)]
    public void MissingAuthenticationReferencesFailAtConfiguration(MailProfileKind kind) {
        var configuration = new MailDeliveryConfiguration {
            Profiles = {
                ["incomplete"] = new() {
                    Profile = new() {
                        Kind = kind, DefaultSender = "sender@example.test",
                        Settings = new() { ["server"] = "smtp.example.test", ["secureSocketOptions"] = "StartTls" }
                    }
                }
            },
            Routes = { ["reports"] = new() { Profiles = new() { "incomplete" } } }
        };
        var error = Assert.Throws<ArgumentException>(() => new MailDeliveryEngine(configuration));
        Assert.Contains("authentication", error.Message);
    }

    [Theory]
    [InlineData("basic", MailSecretNames.Password)]
    [InlineData(" xoauth2 ", MailSecretNames.AccessToken)]
    [InlineData("oauth", MailSecretNames.AccessToken)]
    public void SmtpAuthenticationUsesCanonicalSenderFallbackAndModeAliases(string mode, string secret) {
        var configuration = new MailDeliveryConfiguration {
            Profiles = {
                ["smtp"] = new() {
                    Profile = new() {
                        Kind = MailProfileKind.Smtp, DefaultSender = "sender@example.test",
                        Settings = new() { ["server"] = "smtp.example.test", ["secureSocketOptions"] = "StartTls", ["authMode"] = mode }
                    },
                    Credentials = new() { [secret] = new("environment", "SYNTHETIC_CREDENTIAL") }
                }
            },
            Routes = { ["reports"] = new() { Profiles = new() { "smtp" } } }
        };
        using var engine = new MailDeliveryEngine(configuration);
        Assert.True(engine.ContainsRoute("reports"));
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankSmtpPortUsesTheCanonicalDefault(string port) {
        var configuration = new MailDeliveryConfiguration {
            Profiles = {
                ["smtp"] = new() {
                    Profile = new() {
                        Kind = MailProfileKind.Smtp, DefaultSender = "sender@example.test",
                        Settings = new() { ["server"] = "smtp.example.test", ["port"] = port, ["authenticationEnabled"] = "false" }
                    }
                }
            },
            Routes = { ["reports"] = new() { Profiles = new() { "smtp" } } }
        };
        using var engine = new MailDeliveryEngine(configuration);
        Assert.True(engine.ContainsRoute("reports"));
        Assert.Equal(587, SmtpSessionFactory.GetIntSetting(configuration.Profiles["smtp"].Profile, MailProfileSettingsKeys.Port) ?? 587);
    }

    [Theory]
    [InlineData(MailProfileKind.Smtp, "timeout", "-1", typeof(ArgumentException))]
    [InlineData(MailProfileKind.Smtp, "timeout", "invalid", typeof(InvalidOperationException))]
    [InlineData(MailProfileKind.SendGrid, "retryDelayMilliseconds", "-1", typeof(InvalidOperationException))]
    [InlineData(MailProfileKind.Mailgun, "retryDelayBackoff", "NaN", typeof(InvalidOperationException))]
    [InlineData(MailProfileKind.Ses, "jitterMilliseconds", "-1", typeof(InvalidOperationException))]
    public void InvalidTransportSettingsFailBeforeTheEngineCanSend(MailProfileKind kind, string setting, string value, Type exceptionType) {
        var profile = new MailDeliveryProfile {
            Profile = new() {
                Kind = kind, DefaultSender = "sender@example.test",
                Settings = new() { ["server"] = "smtp.example.test", ["authenticationEnabled"] = "false", [setting] = value }
            },
            Credentials = new() {
                [MailSecretNames.ApiKey] = new("environment", "SYNTHETIC_API_KEY"),
                [MailSecretNames.AccessKeyId] = new("environment", "SYNTHETIC_ACCESS_KEY"),
                [MailSecretNames.SecretAccessKey] = new("environment", "SYNTHETIC_SECRET_KEY")
            }
        };
        var configuration = new MailDeliveryConfiguration {
            Profiles = { ["primary"] = profile },
            Routes = { ["reports"] = new() { Profiles = new() { "primary" } } }
        };
        var error = Assert.Throws(exceptionType, () => new MailDeliveryEngine(configuration));
        Assert.Contains(setting, error.Message);
    }

    [Theory]
    [InlineData("unsupported", "credential", typeof(NotSupportedException))]
    [InlineData("file", "relative-secret.txt", typeof(ArgumentException))]
    public void InvalidDefaultCredentialReferenceIsRejectedButCustomResolverCanOwnItsMeaning(string source, string key, Type exceptionType) {
        var configuration = new MailDeliveryConfiguration {
            Profiles = {
                ["provider"] = new() {
                    Profile = new() { Kind = MailProfileKind.SendGrid, DefaultSender = "sender@example.test" },
                    Credentials = new() { [MailSecretNames.ApiKey] = new(source, key) }
                }
            },
            Routes = { ["reports"] = new() { Profiles = new() { "provider" }, RetryCount = 10 } }
        };
        Assert.Throws(exceptionType, () => new MailDeliveryEngine(configuration));
        using var custom = new MailDeliveryEngine(configuration, new ObservedMissingCredentialResolver());
        Assert.True(custom.ContainsRoute("reports"));
    }

    [Fact]
    public async Task PreparedSmtpMessageKeepsItsAttachmentAfterTheSourceFileIsRemoved() {
        await using var server = new LoopbackSmtpServer();
        string path = Path.Combine(Path.GetTempPath(), "prepared-mail-" + Guid.NewGuid().ToString("N"));
        try {
            File.WriteAllText(path, "synthetic report content");
            var profile = Configuration(server).Profiles["p0"].Profile;
            profile.Id = profile.DisplayName = "prepared";
            profile.SmtpDelivery = new() {
                Servers = new() { new() { Server = "127.0.0.1", Port = server.Port, SecureSocketOptions = MailKit.Security.SecureSocketOptions.None } },
                MaxAttempts = 1
            };
            var draft = Message();
            draft.Attachments.Add(new() { Path = path, FileName = "report.txt" });
            using var mime = await new DraftMimeMessageFactory().CreateAsync(profile, draft);
            File.Delete(path);
            using var session = new SmtpDeliverySession();
            var handler = new SmtpMailSendHandler(new SmtpSessionFactory(), session);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            SendResult result = await handler.SendPreparedAsync(profile, mime, timeout.Token);
            Assert.True(result.Succeeded);
            Assert.Equal(1, server.Accepted);
            using var content = new MemoryStream();
            var attachment = Assert.IsType<MimeKit.MimePart>(Assert.Single(mime.Attachments));
            Assert.NotNull(attachment.Content);
            attachment.Content.DecodeTo(content);
            Assert.Equal("synthetic report content", System.Text.Encoding.UTF8.GetString(content.ToArray()));
        } finally {
            File.Delete(path);
        }
    }
    private static MailDeliveryConfiguration Configuration(params LoopbackSmtpServer[] servers) {
        var configuration = new MailDeliveryConfiguration();
        for (int i = 0; i < servers.Length; i++) {
            configuration.Profiles["p" + i] = new() {
                Profile = new() {
                    Kind = MailProfileKind.Smtp, DefaultSender = "sender@example.test",
                    Settings = new() { ["server"] = "127.0.0.1", ["port"] = servers[i].Port.ToString(),
                        ["secureSocketOptions"] = "None", ["authenticationEnabled"] = "false" }
                }
            };
        }
        configuration.Routes["reports"] = new() { Profiles = configuration.Profiles.Keys.ToList() };
        return configuration;
    }

    private static DraftMessage Message() => new() {
        Subject = "Synthetic report", TextBody = "Report", To = new() { new() { Address = "recipient@example.test" } }
    };

    private sealed class ObservedMissingCredentialResolver : IMailCredentialResolver {
        private readonly MailCredentialResolver _files = new();
        internal List<string?> Seen { get; } = new();
        public async Task<string?> ResolveAsync(MailCredentialReference reference, CancellationToken cancellationToken = default) {
            Seen.Add(await _files.ResolveAsync(reference, cancellationToken));
            return null;
        }
    }
}
