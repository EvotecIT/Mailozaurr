using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;
using Mailozaurr.Definitions;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAsyncWrappersTests
{
    private class FakeConnectClient : ClientSmtp
    {
        public bool ConnectCalled;
        public bool ThrowOnConnect;
        public bool ThrowOnAuthenticate;
        public bool BlockConnectUntilCanceled;
        public bool ConnectCanceled;
        public SecureSocketOptions? LastSecureSocketOptions;
        public CancellationToken LastConnectCancellationToken;
        public string? AuthMechanism;
        public override async Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            LastConnectCancellationToken = cancellationToken;
            if (ThrowOnConnect) {
                throw new InvalidOperationException("connect failed");
            }
            if (BlockConnectUntilCanceled) {
                try {
                    await Task.Delay(global::System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    ConnectCanceled = true;
                    throw;
                }
            }
            ConnectCalled = true;
            LastSecureSocketOptions = options;
        }
        public override Task AuthenticateAsync(SaslMechanism mechanism, CancellationToken cancellationToken = default) {
            if (ThrowOnAuthenticate) {
                throw new InvalidOperationException("auth failed");
            }
            AuthMechanism = mechanism.GetType().Name;
            return Task.CompletedTask;
        }
    }

    private static void SetClient(Smtp smtp, ClientSmtp client)
    {
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, client);
    }

    private static void SetCredential(Smtp smtp, NetworkCredential credential)
    {
        var field = typeof(Smtp).GetField("<Credential>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, credential);
    }

    private static string? GetPoolIdentity(Smtp smtp)
    {
        var field = typeof(Smtp).GetField("_poolIdentity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return field.GetValue(smtp) as string;
    }

    [Fact]
    public async Task ConnectAsync_InvokesClientConnectAsync()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);

        var result = await smtp.ConnectAsync("host", 25);

        Assert.True(fake.ConnectCalled);
        Assert.True(result.Status);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_ReturnsSuccessForOAuthAuthentication()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);

        var result = await smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "user@example.com",
            "oauth-token",
            SecureSocketOptions.StartTls,
            useSsl: false,
            authMode: ProtocolAuthMode.OAuth2);

        Assert.True(result.IsSuccess);
        Assert.Equal(SecureSocketOptions.StartTls, result.SecureSocketOptions);
        Assert.Equal(nameof(SaslMechanismOAuth2), fake.AuthMechanism);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_MapsConnectFailure()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient { ThrowOnConnect = true };
        SetClient(smtp, fake);

        var result = await smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2);

        Assert.False(result.IsSuccess);
        Assert.Equal("connect_failed", result.ErrorCode);
        Assert.True(result.IsTransient);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_MapsValidationFailureAsNonTransient()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);

        var result = await smtp.ConnectAndAuthenticateAsync(
            string.Empty,
            0,
            "user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2);

        Assert.False(result.IsSuccess);
        Assert.Equal("connect_failed", result.ErrorCode);
        Assert.False(result.IsTransient);
        Assert.False(fake.ConnectCalled);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_MapsAuthenticationFailure()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient { ThrowOnAuthenticate = true };
        SetClient(smtp, fake);

        var result = await smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2);

        Assert.False(result.IsSuccess);
        Assert.Equal("auth_failed", result.ErrorCode);
        Assert.False(result.IsTransient);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_ReThrowsAuthFailureWhenErrorActionStop()
    {
        var smtp = new Smtp {
            ErrorAction = ActionPreference.Stop
        };
        var fake = new FakeConnectClient { ThrowOnAuthenticate = true };
        SetClient(smtp, fake);

        await Assert.ThrowsAsync<InvalidOperationException>(() => smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2));
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_DryRunSkipsAuthentication()
    {
        var smtp = new Smtp {
            DryRun = true
        };
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);

        var result = await smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2);

        Assert.True(result.IsSuccess);
        Assert.True(string.IsNullOrWhiteSpace(result.ErrorCode));
        Assert.Null(fake.AuthMechanism);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_ThrowsWhenAlreadyCanceled()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2,
            cancellationToken: cts.Token));

        Assert.False(fake.ConnectCalled);
        Assert.Null(fake.AuthMechanism);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_PropagatesCancellationIntoConnect()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient {
            BlockConnectUntilCanceled = true
        };
        SetClient(smtp, fake);

        using var cts = new CancellationTokenSource();
        var operation = smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2,
            cancellationToken: cts.Token);

        cts.CancelAfter(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        Assert.True(fake.ConnectCanceled);
        Assert.True(fake.LastConnectCancellationToken.CanBeCanceled);
        Assert.Null(fake.AuthMechanism);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_UsesRequestedUsernameForPoolIdentityWhenCredentialWasStale()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);
        SetCredential(smtp, new NetworkCredential("old.user@example.com", "old-secret"));

        var result = await smtp.ConnectAndAuthenticateAsync(
            "host",
            587,
            "new.user@example.com",
            "secret",
            authMode: ProtocolAuthMode.OAuth2);

        var poolIdentity = GetPoolIdentity(smtp);

        Assert.True(result.IsSuccess);
        Assert.NotNull(poolIdentity);
        Assert.StartsWith("new.user@example.com|", poolIdentity!, StringComparison.Ordinal);
        Assert.Null(smtp.ConnectionPoolIdentity);
    }

    [Fact]
    public async Task CreateMessageAsync_AutoEmbedImagesAddsInlineAttachment()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "data");

            var smtp = new Smtp
            {
                AutoEmbedImages = true,
                HtmlBody = $"<img src=\"{tempFile}\">",
                From = "sender@example.com",
                To = new object[] { "recipient@example.com" },
                Subject = "test",
                TextBody = "body"
            };

            await smtp.CreateMessageAsync();

            var inlineAttachments = smtp.InlineAttachments ?? new List<AttachmentDescriptor>();
            Assert.Contains(inlineAttachments.OfType<FileAttachmentDescriptor>(), a => string.Equals(a.FilePath, tempFile, StringComparison.OrdinalIgnoreCase));
            Assert.Contains($"cid:{Path.GetFileName(tempFile)}", smtp.HtmlBody);
            Assert.Contains($"cid:{Path.GetFileName(tempFile)}", smtp.Message.HtmlBody);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task CreateMessageAsync_CancellationTokenPreventsClientInvocation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "data");

            var smtp = new Smtp
            {
                AutoEmbedImages = true,
                HtmlBody = $"<img src=\"{tempFile}\">"
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var originalHtml = smtp.HtmlBody;

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await smtp.CreateMessageAsync(cts.Token));

            Assert.Equal(originalHtml, smtp.HtmlBody);
            var inlineAttachments = smtp.InlineAttachments ?? new List<AttachmentDescriptor>();
            Assert.DoesNotContain(inlineAttachments.OfType<FileAttachmentDescriptor>(), a => string.Equals(a.FilePath, tempFile, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

}
