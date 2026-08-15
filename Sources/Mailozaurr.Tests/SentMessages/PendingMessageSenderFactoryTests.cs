using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public sealed class PendingMessageSenderFactoryTests {
    private sealed class TestSender : IPendingMessageSender {
        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void GetSender_ReturnsRegisteredSender() {
        var sender = new TestSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.SendGrid, sender }
        });

        var record = new PendingMessageRecord { Provider = EmailProvider.SendGrid };

        var result = factory.GetSender(record);

        Assert.Same(sender, result);
    }

    [Fact]
    public void GetSender_ThrowsWhenProviderUnknown() {
        var factory = new PendingMessageSenderFactory();
        var record = new PendingMessageRecord { Provider = (EmailProvider)int.MaxValue };

        var exception = Assert.Throws<NotSupportedException>(() => factory.GetSender(record));
        Assert.Contains("No pending-message sender is registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSender_ThrowsWhenRecordIsNull() {
        var factory = new PendingMessageSenderFactory();

        Assert.Throws<ArgumentNullException>(() => factory.GetSender(null!));
    }

    [Fact]
    public void Resolve_ReturnsRegisteredSender() {
        var sender = new TestSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.Mailgun, sender }
        });

        var result = factory.Resolve(EmailProvider.Mailgun);

        Assert.Same(sender, result);
    }

    [Theory]
    [InlineData(EmailProvider.None, typeof(SmtpPendingMessageSender))]
    [InlineData(EmailProvider.SendGrid, typeof(SendGridPendingMessageSender))]
    [InlineData(EmailProvider.Mailgun, typeof(MailgunPendingMessageSender))]
    [InlineData(EmailProvider.SES, typeof(SesPendingMessageSender))]
    public void Resolve_ReturnsDefaultInternetSenderForKnownProviders(EmailProvider provider, Type expectedType) {
        var factory = new PendingMessageSenderFactory();

        var result = factory.Resolve(provider);

        Assert.IsType(expectedType, result);
    }

    [Fact]
    public void Resolve_ReturnsExplicitProviderLeafSenders() {
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.Gmail] = new GmailPendingMessageSender(),
            [EmailProvider.Graph] = new GraphPendingMessageSender()
        });

        Assert.IsType<GmailPendingMessageSender>(factory.Resolve(EmailProvider.Gmail));
        Assert.IsType<GraphPendingMessageSender>(factory.Resolve(EmailProvider.Graph));
    }

    [Fact]
    public void Resolve_ThrowsForUnknownProvider() {
        var factory = new PendingMessageSenderFactory();

        var exception = Assert.Throws<NotSupportedException>(() => factory.Resolve((EmailProvider)int.MaxValue));
        Assert.Contains("No pending-message sender is registered", exception.Message, StringComparison.Ordinal);
    }
}
