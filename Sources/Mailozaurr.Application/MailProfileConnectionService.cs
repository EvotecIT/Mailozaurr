using MailKit.Net.Imap;
using MailKit;
using Mailozaurr;

namespace Mailozaurr.Application;

/// <summary>
/// Default implementation of live profile connection tests.
/// </summary>
public sealed class MailProfileConnectionService : IMailProfileConnectionService {
    private readonly IMailProfileStore _profileStore;
    private readonly IImapSessionFactory? _imapSessionFactory;
    private readonly IGraphSessionFactory? _graphSessionFactory;
    private readonly IGmailSessionFactory? _gmailSessionFactory;
    private readonly ISmtpSessionFactory? _smtpSessionFactory;
    private readonly Func<ImapClient, CancellationToken, Task> _probeImapAsync;
    private readonly Func<ImapClient, CancellationToken, Task> _probeImapMailboxAsync;
    private readonly Func<GraphSession, CancellationToken, Task> _probeGraphAsync;
    private readonly Func<GraphSession, CancellationToken, Task> _probeGraphMailboxAsync;
    private readonly Func<GmailSession, CancellationToken, Task> _probeGmailAsync;
    private readonly Func<GmailSession, CancellationToken, Task> _probeGmailMailboxAsync;
    private readonly Func<Smtp, CancellationToken, Task> _probeSmtpAsync;

    /// <summary>
    /// Creates a new connection-test service.
    /// </summary>
    public MailProfileConnectionService(
        IMailProfileStore profileStore,
        IImapSessionFactory? imapSessionFactory = null,
        IGraphSessionFactory? graphSessionFactory = null,
        IGmailSessionFactory? gmailSessionFactory = null,
        ISmtpSessionFactory? smtpSessionFactory = null,
        Func<ImapClient, CancellationToken, Task>? probeImapAsync = null,
        Func<ImapClient, CancellationToken, Task>? probeImapMailboxAsync = null,
        Func<GraphSession, CancellationToken, Task>? probeGraphAsync = null,
        Func<GraphSession, CancellationToken, Task>? probeGraphMailboxAsync = null,
        Func<GmailSession, CancellationToken, Task>? probeGmailAsync = null,
        Func<GmailSession, CancellationToken, Task>? probeGmailMailboxAsync = null,
        Func<Smtp, CancellationToken, Task>? probeSmtpAsync = null) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _imapSessionFactory = imapSessionFactory;
        _graphSessionFactory = graphSessionFactory;
        _gmailSessionFactory = gmailSessionFactory;
        _smtpSessionFactory = smtpSessionFactory;
        _probeImapAsync = probeImapAsync ?? DefaultProbeImapAsync;
        _probeImapMailboxAsync = probeImapMailboxAsync ?? DefaultProbeImapMailboxAsync;
        _probeGraphAsync = probeGraphAsync ?? DefaultProbeGraphAsync;
        _probeGraphMailboxAsync = probeGraphMailboxAsync ?? DefaultProbeGraphMailboxAsync;
        _probeGmailAsync = probeGmailAsync ?? DefaultProbeGmailAsync;
        _probeGmailMailboxAsync = probeGmailMailboxAsync ?? DefaultProbeGmailMailboxAsync;
        _probeSmtpAsync = probeSmtpAsync ?? DefaultProbeSmtpAsync;
    }

    /// <inheritdoc />
    public async Task<MailProfileConnectionTestResult> TestAsync(
        string profileId,
        MailProfileConnectionTestScope scope = MailProfileConnectionTestScope.Auto,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            return Failure("profile_required", "Profile id is required.", profileId, MailProfileKind.Unknown, scope, MailProfileConnectionTestScope.Auto);
        }

        var profile = await _profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return Failure("profile_not_found", "Profile was not found.", profileId, MailProfileKind.Unknown, scope, MailProfileConnectionTestScope.Auto);
        }

        var effectiveScope = ResolveScope(profile, scope);
        try {
            return profile.Kind switch {
                MailProfileKind.Imap => await TestImapAsync(profile, scope, effectiveScope, cancellationToken).ConfigureAwait(false),
                MailProfileKind.Graph => await TestGraphAsync(profile, scope, effectiveScope, cancellationToken).ConfigureAwait(false),
                MailProfileKind.Gmail => await TestGmailAsync(profile, scope, effectiveScope, cancellationToken).ConfigureAwait(false),
                MailProfileKind.Smtp => await TestSmtpAsync(profile, scope, effectiveScope, cancellationToken).ConfigureAwait(false),
                _ => Failure("connection_test_not_supported", $"Live connection testing is not supported for profile kind '{profile.Kind}'.", profile.Id, profile.Kind, scope, effectiveScope)
            };
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            return Failure("connection_test_failed", ex.Message, profile.Id, profile.Kind, scope, effectiveScope);
        }
    }

    private async Task<MailProfileConnectionTestResult> TestImapAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        CancellationToken cancellationToken) {
        if (_imapSessionFactory == null) {
            return Failure("connection_test_not_supported", "IMAP connection testing is not configured.", profile.Id, profile.Kind, requestedScope, effectiveScope);
        }

        using var client = await _imapSessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        if (effectiveScope == MailProfileConnectionTestScope.Mailbox) {
            await _probeImapMailboxAsync(client, cancellationToken).ConfigureAwait(false);
            return Success(profile, "openInbox", ResolveTarget(profile), "IMAP mailbox probe succeeded.", requestedScope, effectiveScope);
        }

        await _probeImapAsync(client, cancellationToken).ConfigureAwait(false);
        return Success(profile, "connect", ResolveTarget(profile), "IMAP authentication succeeded.", requestedScope, effectiveScope);
    }

    private async Task<MailProfileConnectionTestResult> TestGraphAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        CancellationToken cancellationToken) {
        if (_graphSessionFactory == null) {
            return Failure("connection_test_not_supported", "Graph connection testing is not configured.", profile.Id, profile.Kind, requestedScope, effectiveScope);
        }

        using var session = await _graphSessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        if (effectiveScope == MailProfileConnectionTestScope.Mailbox) {
            await _probeGraphMailboxAsync(session, cancellationToken).ConfigureAwait(false);
            return Success(profile, "listFolders", session.UserId, "Graph mailbox probe succeeded.", requestedScope, effectiveScope);
        }

        await _probeGraphAsync(session, cancellationToken).ConfigureAwait(false);
        return Success(profile, "connect", session.UserId, effectiveScope == MailProfileConnectionTestScope.Send ? "Graph send preflight succeeded." : "Graph authentication succeeded.", requestedScope, effectiveScope);
    }

    private async Task<MailProfileConnectionTestResult> TestGmailAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        CancellationToken cancellationToken) {
        if (_gmailSessionFactory == null) {
            return Failure("connection_test_not_supported", "Gmail connection testing is not configured.", profile.Id, profile.Kind, requestedScope, effectiveScope);
        }

        using var session = await _gmailSessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        if (effectiveScope == MailProfileConnectionTestScope.Mailbox) {
            await _probeGmailMailboxAsync(session, cancellationToken).ConfigureAwait(false);
            return Success(profile, "listFolders", session.UserId, "Gmail mailbox probe succeeded.", requestedScope, effectiveScope);
        }

        await _probeGmailAsync(session, cancellationToken).ConfigureAwait(false);
        return Success(profile, "getProfile", session.UserId, effectiveScope == MailProfileConnectionTestScope.Send ? "Gmail send preflight succeeded." : "Gmail authentication succeeded.", requestedScope, effectiveScope);
    }

    private async Task<MailProfileConnectionTestResult> TestSmtpAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        CancellationToken cancellationToken) {
        if (_smtpSessionFactory == null) {
            return Failure("connection_test_not_supported", "SMTP connection testing is not configured.", profile.Id, profile.Kind, requestedScope, effectiveScope);
        }

        var smtp = await _smtpSessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        try {
            await _probeSmtpAsync(smtp, cancellationToken).ConfigureAwait(false);
            return Success(
                profile,
                "connect",
                ResolveTarget(profile),
                effectiveScope == MailProfileConnectionTestScope.Send ? "SMTP send preflight succeeded." : "SMTP authentication succeeded.",
                requestedScope,
                effectiveScope);
        } finally {
            smtp.Dispose();
        }
    }

    private static Task DefaultProbeImapAsync(ImapClient client, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (!client.IsConnected || !client.IsAuthenticated) {
            throw new InvalidOperationException("IMAP client is not connected and authenticated.");
        }
        return Task.CompletedTask;
    }

    private static async Task DefaultProbeImapMailboxAsync(ImapClient client, CancellationToken cancellationToken) {
        await DefaultProbeImapAsync(client, cancellationToken).ConfigureAwait(false);
        await client.Inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DefaultProbeGraphAsync(GraphSession session, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        _ = session.UserId;
        await Task.CompletedTask;
    }

    private static async Task DefaultProbeGraphMailboxAsync(GraphSession session, CancellationToken cancellationToken) {
        var folders = await session.Client.ListMailFoldersRecursiveAsync(session.UserId, top: 1, maxRequests: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        _ = folders.Count;
    }

    private static async Task DefaultProbeGmailAsync(GmailSession session, CancellationToken cancellationToken) {
        var profile = await session.Browser.GetProfileAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(profile.EmailAddress)) {
            throw new InvalidOperationException("Gmail profile probe did not return an email address.");
        }
    }

    private static async Task DefaultProbeGmailMailboxAsync(GmailSession session, CancellationToken cancellationToken) {
        var folders = await session.Browser.ListFoldersAsync(cancellationToken).ConfigureAwait(false);
        _ = folders.Count;
    }

    private static Task DefaultProbeSmtpAsync(Smtp smtp, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (smtp == null) {
            throw new ArgumentNullException(nameof(smtp));
        }
        if (!smtp.Client.IsConnected) {
            throw new InvalidOperationException("SMTP client is not connected.");
        }
        return Task.CompletedTask;
    }

    private static string? ResolveTarget(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) &&
            !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            return profile.DefaultMailbox!.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            return profile.DefaultSender!.Trim();
        }
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.UserName, out var userName) &&
            !string.IsNullOrWhiteSpace(userName)) {
            return userName.Trim();
        }

        return null;
    }

    private static MailProfileConnectionTestScope ResolveScope(MailProfile profile, MailProfileConnectionTestScope scope) =>
        scope switch {
            MailProfileConnectionTestScope.Auto => profile.Kind switch {
                MailProfileKind.Imap => MailProfileConnectionTestScope.Mailbox,
                MailProfileKind.Graph => MailProfileConnectionTestScope.Mailbox,
                MailProfileKind.Gmail => MailProfileConnectionTestScope.Mailbox,
                MailProfileKind.Smtp => MailProfileConnectionTestScope.Send,
                _ => MailProfileConnectionTestScope.Auth
            },
            MailProfileConnectionTestScope.Mailbox when profile.Kind == MailProfileKind.Smtp => MailProfileConnectionTestScope.Send,
            MailProfileConnectionTestScope.Send when profile.Kind == MailProfileKind.Imap => MailProfileConnectionTestScope.Auth,
            MailProfileConnectionTestScope.Send when profile.Kind == MailProfileKind.Pop3 => MailProfileConnectionTestScope.Auth,
            _ => scope
        };

    private static MailProfileConnectionTestResult Success(
        MailProfile profile,
        string probe,
        string? target,
        string message,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope executedScope) => new() {
        Succeeded = true,
        Message = message,
        ProfileId = profile.Id,
        ProfileKind = profile.Kind,
        Probe = probe,
        Target = target,
        RequestedScope = requestedScope,
        ExecutedScope = executedScope
    };

    private static MailProfileConnectionTestResult Failure(
        string code,
        string message,
        string? profileId,
        MailProfileKind kind,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope executedScope) => new() {
        Succeeded = false,
        Code = code,
        Message = message,
        ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId,
        ProfileKind = kind,
        RequestedScope = requestedScope,
        ExecutedScope = executedScope
    };
}
