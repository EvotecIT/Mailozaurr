using System;
using System.Collections.Generic;

namespace Mailozaurr;

/// <summary>
/// Creates provider specific <see cref="IPendingMessageSender"/> instances for queued messages.
/// </summary>
public sealed class PendingMessageSenderFactory {
    private readonly IReadOnlyDictionary<EmailProvider, IPendingMessageSender> senders;
    private readonly IPendingMessageSender fallbackSender;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingMessageSenderFactory"/> class.
    /// </summary>
    /// <param name="senders">Known provider specific senders.</param>
    /// <param name="fallbackSender">The sender used when no provider specific sender is registered.</param>
    public PendingMessageSenderFactory(
        IEnumerable<KeyValuePair<EmailProvider, IPendingMessageSender>>? senders = null,
        IPendingMessageSender? fallbackSender = null) {
        this.senders = CreateMap(senders);
        this.fallbackSender = fallbackSender ?? NoopPendingMessageSender.Instance;
    }

    /// <summary>
    /// Gets the sender appropriate for the supplied <paramref name="record"/>.
    /// </summary>
    /// <param name="record">Pending message metadata used to select the sender.</param>
    /// <returns>The sender registered for the provider or a fallback instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <c>null</c>.</exception>
    public IPendingMessageSender GetSender(PendingMessageRecord record) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        return Resolve(record.Provider);
    }

    /// <summary>
    /// Resolves the sender registered for <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">The provider whose sender should be returned.</param>
    /// <returns>The sender registered for the provider or a fallback instance.</returns>
    public IPendingMessageSender Resolve(EmailProvider provider) {
        if (senders.TryGetValue(provider, out var sender)) {
            return sender;
        }

        return fallbackSender;
    }

    private static IReadOnlyDictionary<EmailProvider, IPendingMessageSender> CreateMap(
        IEnumerable<KeyValuePair<EmailProvider, IPendingMessageSender>>? entries) {
        var map = new Dictionary<EmailProvider, IPendingMessageSender>();

        foreach (var defaultEntry in CreateDefaultSenders()) {
            map[defaultEntry.Key] = defaultEntry.Value;
        }

        if (entries != null) {
            foreach (var entry in entries) {
                if (entry.Value == null) {
                    throw new ArgumentException("Sender value cannot be null.", nameof(entries));
                }

                map[entry.Key] = entry.Value;
            }
        }

        return map;
    }

    private static IEnumerable<KeyValuePair<EmailProvider, IPendingMessageSender>> CreateDefaultSenders() {
        yield return new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.None, new SmtpPendingMessageSender());
        yield return new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.SendGrid, new SendGridPendingMessageSender());
        yield return new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.Mailgun, new MailgunPendingMessageSender());
        yield return new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.SES, new SesPendingMessageSender());
        yield return new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.Gmail, new GmailPendingMessageSender());
    }
}
