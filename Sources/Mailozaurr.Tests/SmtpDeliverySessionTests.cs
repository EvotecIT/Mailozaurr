using MailKit.Security;
using MimeKit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class SmtpDeliverySessionTests {
    [Fact]
    public async Task ProfileDeliveryRoute_FailsOverAndReusesConnectionAcrossDrafts() {
        await using var primary = new LoopbackSmtpServer(recipientCode: 450);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        var handler = new SmtpMailSendHandler(new SmtpSessionFactory(), session);
        var profile = new MailProfile {
            Id = "loopback-profile",
            Kind = MailProfileKind.Smtp,
            DefaultSender = "sender@example.invalid",
            SmtpDelivery = Options(primary, secondary),
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.AuthenticationEnabled] = "false",
                [MailProfileSettingsKeys.SecureSocketOptions] = SecureSocketOptions.None.ToString(),
                [MailProfileSettingsKeys.Timeout] = "2000",
                [MailProfileSettingsKeys.RetryCount] = "0",
                [MailProfileSettingsKeys.RetryDelayMilliseconds] = "0",
                [MailProfileSettingsKeys.MaxDelayMilliseconds] = "0",
                [MailProfileSettingsKeys.JitterMilliseconds] = "0"
            }
        };
        var results = new List<SendResult>();
        for (var index = 0; index < 2; index++) {
            results.Add(await handler.SendAsync(profile, new SendMessageRequest {
                ProfileId = profile.Id,
                Message = new DraftMessage {
                    ProfileId = profile.Id,
                    To = new List<MessageRecipient> { new() { Address = "recipient@example.invalid" } },
                    Subject = $"Local draft {index}",
                    TextBody = "Loopback profile route."
                }
            }));
        }
        Assert.All(results, result => {
            Assert.True(result.Succeeded);
            Assert.False(result.DeliveryUnknown);
            Assert.Equal(profile.Id, result.ProfileId);
            Assert.Equal(MailProfileKind.Smtp, result.ProfileKind);
            Assert.Equal("127.0.0.1", result.Server);
            Assert.Equal(secondary.Port, result.Port);
        });
        Assert.Equal(new[] { primary.Port, secondary.Port }, results[0].Attempts.Select(attempt => attempt.Port));
        Assert.Equal(secondary.Port, Assert.Single(results[1].Attempts).Port);
        Assert.Equal(results.Select(result => result.ProviderMessageId), secondary.MessageIds);
        Assert.All(secondary.MessageIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(1, primary.Connections);
        Assert.Equal(1, primary.Recipients);
        Assert.Equal(1, secondary.Connections);
        Assert.Equal(2, secondary.Accepted);
    }

    private static SmtpSessionRequest Request(int timeoutMs = 2000) => new() {
        Authenticate = false, SecureSocketOptions = SecureSocketOptions.None,
        TimeoutMs = timeoutMs, RetryCount = 0, RetryDelayMilliseconds = 0,
        MaxDelayMilliseconds = 0, JitterMilliseconds = 0
    };

    private static SmtpDeliveryOptions Options(params LoopbackSmtpServer[] servers) => new() {
        Servers = servers.Select(server => new SmtpEndpoint {
            Server = "127.0.0.1", Port = server.Port, SecureSocketOptions = SecureSocketOptions.None
        }).ToList()
    };

    private static MimeMessage Message() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.invalid"));
        message.To.Add(MailboxAddress.Parse("recipient@example.invalid"));
        message.Subject = "Loopback protocol test";
        message.Body = new TextPart("plain") { Text = "Local fixture only." };
        return message;
    }

    [Fact]
    public async Task TransientRecipientFailure_FailsOverToAcceptingSecondary() {
        await using var primary = new LoopbackSmtpServer(recipientCode: 450);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var message = Message();
        var result = await session.SendAsync(message, Request(), Options(primary, secondary));
        Assert.True(result.Status);
        Assert.True(result.DeliveryAccepted);
        Assert.False(result.DeliveryUnknown);
        Assert.Equal(new[] { primary.Port, secondary.Port }, result.Attempts.Select(attempt => attempt.Port));
        Assert.True(result.Attempts[0].IsTransient);
        Assert.Equal(1, primary.Recipients);
        Assert.Equal(0, primary.Accepted);
        Assert.Equal(1, secondary.Accepted);
    }

    [Fact]
    public async Task RequiredStartTlsUnavailable_FailsOverBeforeSendingContent() {
        await using var primary = new LoopbackSmtpServer();
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var message = Message();
        var options = Options(primary, secondary);
        options.Servers[0].SecureSocketOptions = SecureSocketOptions.StartTls;
        var result = await session.SendAsync(message, Request(), options);
        Assert.True(result.DeliveryAccepted);
        Assert.Equal(new[] { primary.Port, secondary.Port }, result.Attempts.Select(attempt => attempt.Port));
        Assert.True(result.Attempts[0].IsTransient);
        Assert.False(result.Attempts[0].DeliveryUnknown);
        Assert.Equal(1, primary.Connections);
        Assert.Equal(0, primary.Recipients);
        Assert.Equal(0, primary.ContentSubmissions);
        Assert.Equal(1, secondary.Accepted);
    }

    [Theory]
    [InlineData(454, true)]
    [InlineData(535, false)]
    public async Task AuthenticationReply_ControlsFailoverBeforeSending(int authenticationCode, bool shouldFailOver) {
        await using var primary = new LoopbackSmtpServer(authenticationCode: authenticationCode);
        await using var secondary = new LoopbackSmtpServer(authenticationCode: 235);
        using var session = new SmtpDeliverySession();
        using var message = Message();
        var request = new SmtpSessionRequest {
            Authenticate = true, UserName = "loopback-user", Password = "synthetic-test-password",
            SecureSocketOptions = SecureSocketOptions.None, TimeoutMs = 2000,
            RetryCount = 0, RetryDelayMilliseconds = 0, MaxDelayMilliseconds = 0, JitterMilliseconds = 0
        };
        var result = await session.SendAsync(message, request, Options(primary, secondary));
        Assert.Equal(shouldFailOver, result.DeliveryAccepted);
        Assert.Equal(shouldFailOver, result.Status);
        Assert.False(result.DeliveryUnknown);
        Assert.Equal(shouldFailOver, result.Attempts[0].IsTransient);
        Assert.Equal(1, primary.Authentications);
        Assert.Equal(0, primary.Recipients);
        Assert.Equal(0, primary.ContentSubmissions);
        if (shouldFailOver) {
            Assert.Equal(new[] { primary.Port, secondary.Port }, result.Attempts.Select(attempt => attempt.Port));
            Assert.Equal(1, secondary.Authentications);
            Assert.Equal(1, secondary.Accepted);
        } else {
            Assert.Equal(primary.Port, Assert.Single(result.Attempts).Port);
            Assert.False(result.IsTransient);
            Assert.Equal(0, secondary.Connections);
            Assert.Equal(0, secondary.Accepted);
        }
    }

    [Fact]
    public async Task TransientContentRejection_PreservesMessageIdAcrossRetries() {
        await using var primary = new LoopbackSmtpServer(contentCode: 451);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var message = Message();
        message.MessageId = "rotation-identity@example.invalid";
        var result = await session.SendAsync(message, Request(), Options(primary, secondary));
        Assert.True(result.DeliveryAccepted);
        Assert.Equal(new[] { primary.Port, secondary.Port }, result.Attempts.Select(attempt => attempt.Port));
        Assert.True(result.Attempts[0].IsTransient);
        Assert.Equal("rotation-identity@example.invalid", Assert.Single(primary.MessageIds));
        Assert.Equal("rotation-identity@example.invalid", Assert.Single(secondary.MessageIds));
        Assert.Equal("rotation-identity@example.invalid", result.MessageId);
        Assert.Equal(0, primary.Accepted);
        Assert.Equal(1, secondary.Accepted);
    }

    [Fact]
    public async Task DroppedConfirmationAfterData_ReportsUnknownWithoutRetry() {
        await using var primary = new LoopbackSmtpServer(dropAfterData: true);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var message = Message();
        var result = await session.SendAsync(message, Request(), Options(primary, secondary));
        Assert.False(result.Status);
        Assert.False(result.DeliveryAccepted);
        Assert.True(result.DeliveryUnknown);
        Assert.True(Assert.Single(result.Attempts).DeliveryUnknown);
        Assert.Equal(primary.Port, result.Attempts[0].Port);
        Assert.Equal(1, primary.ContentSubmissions);
        Assert.Equal(message.MessageId, Assert.Single(primary.MessageIds));
        Assert.Equal(0, secondary.Connections);
        Assert.Equal(0, secondary.Accepted);
    }

    [Fact]
    public async Task SilentPrimaryGreeting_TimesOutAndFailsOver() {
        await using var primary = new LoopbackSmtpServer(silentGreeting: true);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var message = Message();
        var result = await session.SendAsync(message, Request(500), Options(primary, secondary));
        Assert.True(result.DeliveryAccepted);
        Assert.Equal(1, primary.Connections);
        Assert.Equal(0, primary.Recipients);
        Assert.Equal(new[] { primary.Port, secondary.Port }, result.Attempts.Select(attempt => attempt.Port));
        Assert.True(result.Attempts[0].IsTransient);
        Assert.False(result.Attempts[0].DeliveryUnknown);
        Assert.Equal(1, secondary.Accepted);
    }

    [Theory]
    [InlineData(550, 250, 0)]
    [InlineData(250, 554, 1)]
    public async Task PermanentSmtpRejection_DoesNotConnectToSecondary(int recipientCode, int contentCode, int contentSubmissions) {
        await using var primary = new LoopbackSmtpServer(recipientCode, contentCode);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var message = Message();
        var result = await session.SendAsync(message, Request(), Options(primary, secondary));
        Assert.False(result.Status);
        Assert.False(result.DeliveryAccepted);
        Assert.False(result.IsTransient);
        Assert.False(result.DeliveryUnknown);
        Assert.Equal(primary.Port, Assert.Single(result.Attempts).Port);
        Assert.Equal(1, primary.Recipients);
        Assert.Equal(contentSubmissions, primary.ContentSubmissions);
        Assert.Equal(0, primary.Accepted);
        Assert.Equal(0, secondary.Connections);
    }

    [Fact]
    public async Task BatchCooldown_SkipsFailedPrimaryAndReusesHealthyConnection() {
        await using var primary = new LoopbackSmtpServer(recipientCode: 450);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        var request = Request();
        var options = Options(primary, secondary);
        using var first = Message();
        using var second = Message();
        Assert.True((await session.SendAsync(first, request, options)).DeliveryAccepted);
        var result = await session.SendAsync(second, Request(), options);
        Assert.True(result.DeliveryAccepted);
        Assert.Equal(secondary.Port, Assert.Single(result.Attempts).Port);
        Assert.Equal(1, primary.Connections);
        Assert.Equal(1, primary.Recipients);
        Assert.Equal(1, secondary.Connections);
        Assert.Equal(2, secondary.Accepted);
    }

    [Fact]
    public async Task RoundRobin_AlternatesAcceptingEndpoints() {
        await using var first = new LoopbackSmtpServer();
        await using var second = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        var request = Request();
        var options = Options(first, second);
        options.Strategy = SmtpServerStrategy.RoundRobin;
        var ports = new List<int>();
        for (var index = 0; index < 4; index++) {
            using var message = Message();
            var result = await session.SendAsync(message, request, options);
            Assert.True(result.DeliveryAccepted);
            ports.Add(Assert.Single(result.Attempts).Port);
        }
        Assert.Equal(new[] { first.Port, second.Port, first.Port, second.Port }, ports);
        Assert.Equal(2, first.Accepted);
        Assert.Equal(2, second.Accepted);
    }

    [Fact]
    public async Task Random_OnlyDeliversToConfiguredEndpoints() {
        await using var first = new LoopbackSmtpServer();
        await using var second = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        var request = Request();
        var options = Options(first, second);
        options.Strategy = SmtpServerStrategy.Random;
        var receipts = new Dictionary<int, int> { [first.Port] = 0, [second.Port] = 0 };
        for (var index = 0; index < 8; index++) {
            using var message = Message();
            var result = await session.SendAsync(message, request, options);
            Assert.True(result.DeliveryAccepted);
            var attempt = Assert.Single(result.Attempts);
            Assert.Contains(attempt.Port, receipts.Keys);
            Assert.Equal("127.0.0.1", attempt.Server);
            receipts[attempt.Port]++;
        }
        Assert.Equal(receipts[first.Port], first.Accepted);
        Assert.Equal(receipts[second.Port], second.Accepted);
    }

    [Fact]
    public async Task AllTransientFailures_StopAtGlobalAttemptBudget() {
        await using var first = new LoopbackSmtpServer(recipientCode: 450);
        await using var second = new LoopbackSmtpServer(recipientCode: 451);
        using var session = new SmtpDeliverySession();
        var options = Options(first, second);
        options.CircuitBreakMinutes = 0;
        options.MaxAttempts = 3;
        using var message = Message();
        var result = await session.SendAsync(message, Request(), options);
        Assert.False(result.Status);
        Assert.False(result.DeliveryAccepted);
        Assert.False(result.DeliveryUnknown);
        Assert.True(result.IsTransient);
        Assert.Equal(new[] { first.Port, second.Port, first.Port }, result.Attempts.Select(attempt => attempt.Port));
        Assert.All(result.Attempts, attempt => Assert.True(attempt.IsTransient));
        Assert.Equal(3, first.Recipients + second.Recipients);
        Assert.Equal(0, first.Accepted + second.Accepted);
    }

    [Fact]
    public async Task CancellationDuringGreeting_DoesNotRetrySecondary() {
        await using var primary = new LoopbackSmtpServer(silentGreeting: true);
        await using var secondary = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var message = Message();
        var send = session.SendAsync(message, Request(5000), Options(primary, secondary), cancellation.Token);
        var observed = await Task.WhenAny(primary.Connected, send, Task.Delay(TimeSpan.FromSeconds(5)));
        cancellation.Cancel();
        Assert.Same(primary.Connected, observed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await send);
        Assert.Equal(1, primary.Connections);
        Assert.Equal(0, primary.Recipients);
        Assert.Equal(0, secondary.Connections);
    }
}
