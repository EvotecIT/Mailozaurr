using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using System.Diagnostics;
using System.Net;
using static Mailozaurr.MailProfileDiagnosticEvidenceFactory;

namespace Mailozaurr;

/// <summary>
/// Default implementation of live profile connection tests.
/// </summary>
public sealed class MailProfileConnectionService : IMailProfileConnectionService {
    private readonly IMailProfileStore _profileStore;
    private readonly IImapSessionFactory? _imapSessionFactory;
    private readonly IPop3SessionFactory? _pop3SessionFactory;
    private readonly IGraphSessionFactory? _graphSessionFactory;
    private readonly IGmailSessionFactory? _gmailSessionFactory;
    private readonly ISmtpSessionFactory? _smtpSessionFactory;
    private readonly Func<ImapClient, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probeImapAsync;
    private readonly Func<ImapClient, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probeImapMailboxAsync;
    private readonly Func<Pop3Client, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probePop3Async;
    private readonly Func<Pop3Client, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probePop3MailboxAsync;
    private readonly Func<GraphSession, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probeGraphAsync;
    private readonly Func<GraphSession, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probeGraphMailboxAsync;
    private readonly Func<GmailSession, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probeGmailAsync;
    private readonly Func<GmailSession, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probeGmailMailboxAsync;
    private readonly Func<Smtp, CancellationToken, Task<MailProfileDiagnosticEvidence?>> _probeSmtpAsync;

    /// <summary>
    /// Creates a connection-test service while preserving the original constructor contract.
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
        Func<Smtp, CancellationToken, Task>? probeSmtpAsync = null)
        : this(
            profileStore,
            pop3SessionFactory: null,
            imapSessionFactory,
            graphSessionFactory,
            gmailSessionFactory,
            smtpSessionFactory,
            probeImapAsync,
            probeImapMailboxAsync,
            probePop3Async: null,
            probePop3MailboxAsync: null,
            probeGraphAsync,
            probeGraphMailboxAsync,
            probeGmailAsync,
            probeGmailMailboxAsync,
            probeSmtpAsync,
            initialize: true) {
    }

    /// <summary>
    /// Creates a connection-test service with explicit POP3 support for direct library composition.
    /// </summary>
    public static MailProfileConnectionService CreateWithPop3(
        IMailProfileStore profileStore,
        IPop3SessionFactory pop3SessionFactory,
        IImapSessionFactory? imapSessionFactory,
        IGraphSessionFactory? graphSessionFactory,
        IGmailSessionFactory? gmailSessionFactory,
        ISmtpSessionFactory? smtpSessionFactory,
        Func<ImapClient, CancellationToken, Task>? probeImapAsync = null,
        Func<ImapClient, CancellationToken, Task>? probeImapMailboxAsync = null,
        Func<Pop3Client, CancellationToken, Task>? probePop3Async = null,
        Func<Pop3Client, CancellationToken, Task>? probePop3MailboxAsync = null,
        Func<GraphSession, CancellationToken, Task>? probeGraphAsync = null,
        Func<GraphSession, CancellationToken, Task>? probeGraphMailboxAsync = null,
        Func<GmailSession, CancellationToken, Task>? probeGmailAsync = null,
        Func<GmailSession, CancellationToken, Task>? probeGmailMailboxAsync = null,
        Func<Smtp, CancellationToken, Task>? probeSmtpAsync = null) =>
        new MailProfileConnectionService(
            profileStore,
            pop3SessionFactory ?? throw new ArgumentNullException(nameof(pop3SessionFactory)),
            imapSessionFactory,
            graphSessionFactory,
            gmailSessionFactory,
            smtpSessionFactory,
            probeImapAsync,
            probeImapMailboxAsync,
            probePop3Async,
            probePop3MailboxAsync,
            probeGraphAsync,
            probeGraphMailboxAsync,
            probeGmailAsync,
            probeGmailMailboxAsync,
            probeSmtpAsync,
            initialize: true);

    private MailProfileConnectionService(
        IMailProfileStore profileStore,
        IPop3SessionFactory? pop3SessionFactory,
        IImapSessionFactory? imapSessionFactory,
        IGraphSessionFactory? graphSessionFactory,
        IGmailSessionFactory? gmailSessionFactory,
        ISmtpSessionFactory? smtpSessionFactory,
        Func<ImapClient, CancellationToken, Task>? probeImapAsync,
        Func<ImapClient, CancellationToken, Task>? probeImapMailboxAsync,
        Func<Pop3Client, CancellationToken, Task>? probePop3Async,
        Func<Pop3Client, CancellationToken, Task>? probePop3MailboxAsync,
        Func<GraphSession, CancellationToken, Task>? probeGraphAsync,
        Func<GraphSession, CancellationToken, Task>? probeGraphMailboxAsync,
        Func<GmailSession, CancellationToken, Task>? probeGmailAsync,
        Func<GmailSession, CancellationToken, Task>? probeGmailMailboxAsync,
        Func<Smtp, CancellationToken, Task>? probeSmtpAsync,
        bool initialize) {
        _ = initialize;
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _pop3SessionFactory = pop3SessionFactory;
        _imapSessionFactory = imapSessionFactory;
        _graphSessionFactory = graphSessionFactory;
        _gmailSessionFactory = gmailSessionFactory;
        _smtpSessionFactory = smtpSessionFactory;
        _probeImapAsync = probeImapAsync == null
            ? DefaultProbeImapAsync
            : WrapProbe(probeImapAsync, CreateImapEvidence);
        _probeImapMailboxAsync = probeImapMailboxAsync == null
            ? DefaultProbeImapMailboxAsync
            : WrapProbe(probeImapMailboxAsync, CreateImapEvidence);
        _probePop3Async = probePop3Async == null
            ? DefaultProbePop3Async
            : WrapProbe(probePop3Async, CreatePop3Evidence);
        _probePop3MailboxAsync = probePop3MailboxAsync == null
            ? DefaultProbePop3MailboxAsync
            : WrapProbe(probePop3MailboxAsync, CreatePop3Evidence);
        _probeGraphAsync = probeGraphAsync == null
            ? DefaultProbeGraphAsync
            : WrapProbe(probeGraphAsync, CreateGraphSessionEvidence);
        _probeGraphMailboxAsync = probeGraphMailboxAsync == null
            ? DefaultProbeGraphMailboxAsync
            : WrapProbe(probeGraphMailboxAsync, CreateGraphSessionEvidence);
        _probeGmailAsync = probeGmailAsync == null
            ? DefaultProbeGmailAsync
            : WrapProbe(probeGmailAsync, CreateGmailSessionEvidence);
        _probeGmailMailboxAsync = probeGmailMailboxAsync == null
            ? DefaultProbeGmailMailboxAsync
            : WrapProbe(probeGmailMailboxAsync, CreateGmailSessionEvidence);
        _probeSmtpAsync = probeSmtpAsync == null
            ? DefaultProbeSmtpAsync
            : WrapProbe(probeSmtpAsync, CreateSmtpEvidence);
    }

    /// <inheritdoc />
    public async Task<MailProfileConnectionTestResult> TestAsync(
        string profileId,
        MailProfileConnectionTestScope scope = MailProfileConnectionTestScope.Auto,
        CancellationToken cancellationToken = default) {
        var stages = new List<MailProfileConnectionTestStage>();
        if (string.IsNullOrWhiteSpace(profileId)) {
            AddStage(stages, MailProfileConnectionTestPhase.Profile, false, "resolveProfile", null, 0,
                "profile_required", "Profile id is required.");
            return Failure("profile_required", "Profile id is required.", profileId, MailProfileKind.Unknown, scope, MailProfileConnectionTestScope.Auto, stages);
        }

        var timer = Stopwatch.StartNew();
        var profile = await _profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        timer.Stop();
        if (profile == null) {
            AddStage(stages, MailProfileConnectionTestPhase.Profile, false, "resolveProfile", profileId.Trim(), timer.ElapsedMilliseconds,
                "profile_not_found", "Profile was not found.");
            return Failure("profile_not_found", "Profile was not found.", profileId, MailProfileKind.Unknown, scope, MailProfileConnectionTestScope.Auto, stages);
        }

        var effectiveScope = ResolveScope(profile, scope);
        AddStage(stages, MailProfileConnectionTestPhase.Profile, true, "resolveProfile", profile.Id, timer.ElapsedMilliseconds,
            null, $"Profile '{profile.Id}' resolved as {profile.Kind}.");

        return profile.Kind switch {
            MailProfileKind.Imap => await TestImapAsync(profile, scope, effectiveScope, stages, cancellationToken).ConfigureAwait(false),
            MailProfileKind.Pop3 => await TestPop3Async(profile, scope, effectiveScope, stages, cancellationToken).ConfigureAwait(false),
            MailProfileKind.Graph => await TestGraphAsync(profile, scope, effectiveScope, stages, cancellationToken).ConfigureAwait(false),
            MailProfileKind.Gmail => await TestGmailAsync(profile, scope, effectiveScope, stages, cancellationToken).ConfigureAwait(false),
            MailProfileKind.Smtp => await TestSmtpAsync(profile, scope, effectiveScope, stages, cancellationToken).ConfigureAwait(false),
            _ => Unsupported(profile, scope, effectiveScope, stages)
        };
    }

    private Task<MailProfileConnectionTestResult> TestImapAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        List<MailProfileConnectionTestStage> stages,
        CancellationToken cancellationToken) {
        if (_imapSessionFactory == null) {
            return Task.FromResult(Unsupported(profile, requestedScope, effectiveScope, stages, "IMAP connection testing is not configured."));
        }

        var mailbox = effectiveScope == MailProfileConnectionTestScope.Mailbox;
        return ExecuteSessionAsync(
            profile, requestedScope, effectiveScope, stages,
            token => _imapSessionFactory.ConnectAsync(profile, token),
            session => session.Dispose(),
            mailbox ? _probeImapMailboxAsync : _probeImapAsync,
            mailbox ? MailProfileConnectionTestPhase.Mailbox : MailProfileConnectionTestPhase.Probe,
            mailbox ? "openInbox" : "connect",
            ResolveTarget(profile),
            "IMAP authenticated session created.",
            mailbox ? "IMAP mailbox probe succeeded." : "IMAP authentication succeeded.",
            cancellationToken,
            sessionEvidenceResolver: CreateImapEvidence);
    }

    private Task<MailProfileConnectionTestResult> TestPop3Async(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        List<MailProfileConnectionTestStage> stages,
        CancellationToken cancellationToken) {
        if (_pop3SessionFactory == null) {
            return Task.FromResult(Unsupported(profile, requestedScope, effectiveScope, stages, "POP3 connection testing is not configured."));
        }

        var mailbox = effectiveScope == MailProfileConnectionTestScope.Mailbox;
        return ExecuteSessionAsync(
            profile, requestedScope, effectiveScope, stages,
            token => _pop3SessionFactory.ConnectAsync(profile, token),
            session => session.Dispose(),
            mailbox ? _probePop3MailboxAsync : _probePop3Async,
            mailbox ? MailProfileConnectionTestPhase.Mailbox : MailProfileConnectionTestPhase.Probe,
            mailbox ? "inspectMailbox" : "connect",
            ResolveTarget(profile),
            "POP3 authenticated session created.",
            mailbox ? "POP3 mailbox inspection succeeded." : "POP3 authentication succeeded.",
            cancellationToken,
            sessionEvidenceResolver: CreatePop3Evidence);
    }

    private Task<MailProfileConnectionTestResult> TestGraphAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        List<MailProfileConnectionTestStage> stages,
        CancellationToken cancellationToken) {
        if (_graphSessionFactory == null) {
            return Task.FromResult(Unsupported(profile, requestedScope, effectiveScope, stages, "Graph connection testing is not configured."));
        }

        var mailbox = effectiveScope == MailProfileConnectionTestScope.Mailbox;
        var send = effectiveScope == MailProfileConnectionTestScope.Send;
        return ExecuteSessionAsync(
            profile, requestedScope, effectiveScope, stages,
            token => _graphSessionFactory.ConnectAsync(profile, token),
            session => session.Dispose(),
            mailbox ? _probeGraphMailboxAsync : _probeGraphAsync,
            mailbox ? MailProfileConnectionTestPhase.Mailbox : send ? MailProfileConnectionTestPhase.SendPreflight : MailProfileConnectionTestPhase.Probe,
            mailbox ? "listFolders" : "inspectMailAccess",
            ResolveTarget(profile),
            "Graph credential session created.",
            mailbox ? "Graph mailbox probe succeeded." : send ? "Graph send preflight completed." : "Graph mail-access probe succeeded.",
            cancellationToken,
            session => session.UserId,
            CreateGraphSessionEvidence);
    }

    private Task<MailProfileConnectionTestResult> TestGmailAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        List<MailProfileConnectionTestStage> stages,
        CancellationToken cancellationToken) {
        if (_gmailSessionFactory == null) {
            return Task.FromResult(Unsupported(profile, requestedScope, effectiveScope, stages, "Gmail connection testing is not configured."));
        }

        var mailbox = effectiveScope == MailProfileConnectionTestScope.Mailbox;
        var send = effectiveScope == MailProfileConnectionTestScope.Send;
        return ExecuteSessionAsync(
            profile, requestedScope, effectiveScope, stages,
            token => _gmailSessionFactory.ConnectAsync(profile, token),
            session => session.Dispose(),
            mailbox ? _probeGmailMailboxAsync : _probeGmailAsync,
            mailbox ? MailProfileConnectionTestPhase.Mailbox : send ? MailProfileConnectionTestPhase.SendPreflight : MailProfileConnectionTestPhase.Probe,
            mailbox ? "listFolders" : "getProfile",
            ResolveTarget(profile),
            "Gmail credential session created.",
            mailbox ? "Gmail mailbox probe succeeded." : send ? "Gmail send preflight completed." : "Gmail provider probe succeeded.",
            cancellationToken,
            session => session.UserId,
            CreateGmailSessionEvidence);
    }

    private Task<MailProfileConnectionTestResult> TestSmtpAsync(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        List<MailProfileConnectionTestStage> stages,
        CancellationToken cancellationToken) {
        if (_smtpSessionFactory == null) {
            return Task.FromResult(Unsupported(profile, requestedScope, effectiveScope, stages, "SMTP connection testing is not configured."));
        }

        var send = effectiveScope == MailProfileConnectionTestScope.Send;
        return ExecuteSessionAsync(
            profile, requestedScope, effectiveScope, stages,
            token => _smtpSessionFactory.ConnectAsync(profile, token),
            session => session.Dispose(),
            _probeSmtpAsync,
            send ? MailProfileConnectionTestPhase.SendPreflight : MailProfileConnectionTestPhase.Probe,
            "inspectSession",
            ResolveTarget(profile),
            "SMTP session created.",
            send ? "SMTP send preflight completed." : "SMTP session probe succeeded.",
            cancellationToken,
            sessionEvidenceResolver: CreateSmtpEvidence);
    }

    private static async Task<MailProfileConnectionTestResult> ExecuteSessionAsync<TSession>(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        List<MailProfileConnectionTestStage> stages,
        Func<CancellationToken, Task<TSession>> connectAsync,
        Action<TSession> dispose,
        Func<TSession, CancellationToken, Task<MailProfileDiagnosticEvidence?>> probeAsync,
        MailProfileConnectionTestPhase probePhase,
        string probe,
        string? target,
        string sessionMessage,
        string successMessage,
        CancellationToken cancellationToken,
        Func<TSession, string?>? targetResolver = null,
        Func<TSession, MailProfileDiagnosticEvidence?>? sessionEvidenceResolver = null) {
        TSession session;
        var timer = Stopwatch.StartNew();
        try {
            session = await connectAsync(cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            timer.Stop();
            AddStage(stages, MailProfileConnectionTestPhase.Session, false, "connect", target, timer.ElapsedMilliseconds,
                "connection_test_failed", ex.Message);
            return Failure("connection_test_failed", ex.Message, profile.Id, profile.Kind, requestedScope, effectiveScope, stages);
        }

        MailProfileConnectionTestResult result;
        try {
            timer.Stop();
            target = targetResolver?.Invoke(session) ?? target;
            AddStage(stages, MailProfileConnectionTestPhase.Session, true, "connect", target, timer.ElapsedMilliseconds,
                null, sessionMessage, sessionEvidenceResolver?.Invoke(session));

            timer.Restart();
            var evidence = await probeAsync(session, cancellationToken).ConfigureAwait(false);
            if (probePhase == MailProfileConnectionTestPhase.SendPreflight) {
                evidence ??= new MailProfileDiagnosticEvidence();
                evidence.Preflight ??= CreateSendPreflightEvidence(profile.Kind, evidence);
            }
            if (evidence?.Preflight?.Ready == false) {
                timer.Stop();
                const string code = "send_preflight_not_ready";
                var message = evidence.Preflight.Detail ?? "The available evidence does not support attempting the send operation.";
                AddStage(stages, probePhase, false, probe, target, timer.ElapsedMilliseconds, code, message, evidence);
                result = Failure(code, message, profile.Id, profile.Kind, requestedScope, effectiveScope, stages);
            } else {
                timer.Stop();
                AddStage(stages, probePhase, true, probe, target, timer.ElapsedMilliseconds, null, successMessage, evidence);
                result = Success(profile, probe, target, successMessage, requestedScope, effectiveScope, stages);
            }
        } catch (OperationCanceledException) {
            try {
                dispose(session);
            } catch (Exception) {
                // Preserve cancellation as the controlling outcome.
            }
            throw;
        } catch (Exception ex) {
            timer.Stop();
            AddStage(stages, probePhase, false, probe, target, timer.ElapsedMilliseconds,
                "connection_test_failed", ex.Message);
            result = Failure("connection_test_failed", ex.Message, profile.Id, profile.Kind, requestedScope, effectiveScope, stages);
        }

        timer.Restart();
        try {
            dispose(session);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            timer.Stop();
            AddStage(stages, MailProfileConnectionTestPhase.Cleanup, false, "dispose", target, timer.ElapsedMilliseconds,
                "connection_test_failed", ex.Message);
            if (result.Succeeded) {
                return Failure("connection_test_failed", ex.Message, profile.Id, profile.Kind, requestedScope, effectiveScope, stages);
            }
        }
        return result;
    }

    private static Task<MailProfileDiagnosticEvidence?> DefaultProbeImapAsync(ImapClient client, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!client.IsConnected || !client.IsAuthenticated) {
            throw new InvalidOperationException("IMAP client is not connected and authenticated.");
        }
        return Task.FromResult<MailProfileDiagnosticEvidence?>(CreateImapEvidence(client));
    }

    private static async Task<MailProfileDiagnosticEvidence?> DefaultProbeImapMailboxAsync(ImapClient client, CancellationToken cancellationToken) {
        await DefaultProbeImapAsync(client, cancellationToken).ConfigureAwait(false);
        var inbox = client.Inbox ?? throw new InvalidOperationException("IMAP client does not expose an inbox folder.");
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
        var evidence = CreateImapEvidence(client);
        evidence.Mailbox = new MailProfileMailboxEvidence {
            MessageCount = inbox.Count,
            UnreadCount = inbox.Unread,
            ChangeCursor = "uidValidity:" + inbox.UidValidity.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return evidence;
    }

    private static Task<MailProfileDiagnosticEvidence?> DefaultProbePop3Async(Pop3Client client, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!client.IsConnected || !client.IsAuthenticated) {
            throw new InvalidOperationException("POP3 client is not connected and authenticated.");
        }
        return Task.FromResult<MailProfileDiagnosticEvidence?>(CreatePop3Evidence(client));
    }

    private static async Task<MailProfileDiagnosticEvidence?> DefaultProbePop3MailboxAsync(Pop3Client client, CancellationToken cancellationToken) {
        await DefaultProbePop3Async(client, cancellationToken).ConfigureAwait(false);
        if (client.Count > 0) {
            try {
                await client.GetMessageHeadersAsync(client.Count - 1, cancellationToken).ConfigureAwait(false);
            } catch (NotSupportedException) {
                await client.GetMessageAsync(client.Count - 1, cancellationToken).ConfigureAwait(false);
            }
        }
        var evidence = CreatePop3Evidence(client);
        evidence.Mailbox = new MailProfileMailboxEvidence {
            MessageCount = client.Count
        };
        return evidence;
    }

    private static async Task<MailProfileDiagnosticEvidence?> DefaultProbeGraphAsync(GraphSession session, CancellationToken cancellationToken) {
        await session.Client.ListMailFoldersRecursiveAsync(
            session.UserId,
            top: 1,
            maxRequests: 1,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var evidence = CreateGraphEvidence(session);
        await TryAttachGraphIdentityAsync(session, evidence, cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    private static async Task<MailProfileDiagnosticEvidence?> DefaultProbeGraphMailboxAsync(GraphSession session, CancellationToken cancellationToken) {
        var folders = await session.Client.ListMailFoldersRecursiveAsync(
            session.UserId,
            top: 1,
            maxRequests: 1,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var evidence = CreateGraphEvidence(session);
        evidence.Mailbox = new MailProfileMailboxEvidence {
            FolderCount = folders.Count
        };
        await TryAttachGraphIdentityAsync(session, evidence, cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    private static async Task<MailProfileDiagnosticEvidence?> DefaultProbeGmailAsync(GmailSession session, CancellationToken cancellationToken) {
        try {
            var profile = await session.Browser.GetProfileWithoutRefreshAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(profile.EmailAddress)) {
                throw new InvalidOperationException("Gmail profile probe did not return an email address.");
            }
            return CreateGmailEvidence(profile);
        } catch (GmailAuthenticationException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) {
            var evidence = CreateGmailCapabilityEvidence(
                "Gmail recognized the credential but the users.getProfile endpoint was outside its granted scope. OAuth scope metadata was not available and send readiness was not inferred.");
            evidence.IdentityUnavailableReason = "Gmail users.getProfile returned 403 Forbidden for this credential scope.";
            return evidence;
        }
    }

    private static async Task<MailProfileDiagnosticEvidence?> DefaultProbeGmailMailboxAsync(GmailSession session, CancellationToken cancellationToken) {
        var folders = await session.Browser.ListFoldersAsync(cancellationToken).ConfigureAwait(false);
        var evidence = CreateGmailCapabilityEvidence(
            "The Gmail labels endpoint verified mailbox access, but OAuth scope metadata was not available and was not inferred.");
        evidence.Mailbox = new MailProfileMailboxEvidence { FolderCount = folders.Count };
        try {
            var profile = await session.Browser.GetProfileWithoutRefreshAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(profile.EmailAddress)) {
                ApplyGmailProfile(evidence, profile);
            } else {
                evidence.IdentityUnavailableReason = "Gmail users.getProfile returned no email address.";
            }
        } catch (GmailAuthenticationException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) {
            evidence.IdentityUnavailableReason = "Gmail users.getProfile returned 403 Forbidden for this credential scope.";
        }
        return evidence;
    }

    private static async Task TryAttachGraphIdentityAsync(
        GraphSession session,
        MailProfileDiagnosticEvidence evidence,
        CancellationToken cancellationToken) {
        try {
            var identity = await session.Client.GetMailboxIdentityAsync(session.UserId, cancellationToken).ConfigureAwait(false);
            var identityEvidence = CreateGraphEvidence(session, identity);
            evidence.Identity = identityEvidence.Identity;
        } catch (GraphApiException ex) {
            evidence.IdentityUnavailableReason = $"Microsoft Graph users endpoint returned {(int)ex.StatusCode} ({ex.StatusCode}); mail capability evidence remains valid.";
        }
    }

    private static Task<MailProfileDiagnosticEvidence?> DefaultProbeSmtpAsync(Smtp smtp, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!smtp.Client.IsConnected) {
            throw new InvalidOperationException("SMTP client is not connected.");
        }
        return Task.FromResult<MailProfileDiagnosticEvidence?>(CreateSmtpEvidence(smtp));
    }

    private static Func<TSession, CancellationToken, Task<MailProfileDiagnosticEvidence?>> WrapProbe<TSession>(
        Func<TSession, CancellationToken, Task> probe,
        Func<TSession, MailProfileDiagnosticEvidence> evidenceFactory) => async (session, cancellationToken) => {
            await probe(session, cancellationToken).ConfigureAwait(false);
            return evidenceFactory(session);
        };

    private static MailProfileConnectionTestResult Unsupported(
        MailProfile profile,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope effectiveScope,
        List<MailProfileConnectionTestStage> stages,
        string? message = null) {
        message ??= $"Live connection testing is not supported for profile kind '{profile.Kind}'.";
        AddStage(stages, MailProfileConnectionTestPhase.Session, false, "connect", ResolveTarget(profile), 0,
            "connection_test_not_supported", message);
        return Failure("connection_test_not_supported", message, profile.Id, profile.Kind, requestedScope, effectiveScope, stages);
    }

    private static string? ResolveTarget(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) && !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            return profile.DefaultMailbox!.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            return profile.DefaultSender!.Trim();
        }
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.UserName, out var userName) && !string.IsNullOrWhiteSpace(userName)) {
            return userName.Trim();
        }
        return null;
    }

    private static MailProfileConnectionTestScope ResolveScope(MailProfile profile, MailProfileConnectionTestScope scope) =>
        scope switch {
            MailProfileConnectionTestScope.Auto => profile.Kind switch {
                MailProfileKind.Imap => MailProfileConnectionTestScope.Mailbox,
                MailProfileKind.Pop3 => MailProfileConnectionTestScope.Mailbox,
                MailProfileKind.Graph => MailProfileConnectionTestScope.Mailbox,
                MailProfileKind.Gmail => MailProfileConnectionTestScope.Mailbox,
                MailProfileKind.Smtp => MailProfileConnectionTestScope.Send,
                _ => MailProfileConnectionTestScope.Auth
            },
            MailProfileConnectionTestScope.Mailbox when profile.Kind == MailProfileKind.Smtp => MailProfileConnectionTestScope.Send,
            MailProfileConnectionTestScope.Send when profile.Kind is MailProfileKind.Imap or MailProfileKind.Pop3 => MailProfileConnectionTestScope.Auth,
            _ => scope
        };

    private static void AddStage(
        ICollection<MailProfileConnectionTestStage> stages,
        MailProfileConnectionTestPhase phase,
        bool succeeded,
        string probe,
        string? target,
        long durationMilliseconds,
        string? code,
        string message,
        MailProfileDiagnosticEvidence? evidence = null) => stages.Add(new MailProfileConnectionTestStage {
            Phase = phase,
            Succeeded = succeeded,
            Probe = probe,
            Target = target,
            DurationMilliseconds = durationMilliseconds,
            Code = code,
            Message = message,
            Evidence = evidence
        });

    private static MailProfileConnectionTestResult Success(
        MailProfile profile,
        string probe,
        string? target,
        string message,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope executedScope,
        List<MailProfileConnectionTestStage> stages) => new() {
            Succeeded = true,
            Message = message,
            ProfileId = profile.Id,
            ProfileKind = profile.Kind,
            Probe = probe,
            Target = target,
            RequestedScope = requestedScope,
            ExecutedScope = executedScope,
            Stages = stages
        };

    private static MailProfileConnectionTestResult Failure(
        string code,
        string message,
        string? profileId,
        MailProfileKind kind,
        MailProfileConnectionTestScope requestedScope,
        MailProfileConnectionTestScope executedScope,
        List<MailProfileConnectionTestStage> stages) => new() {
            Succeeded = false,
            Code = code,
            Message = message,
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId,
            ProfileKind = kind,
            RequestedScope = requestedScope,
            ExecutedScope = executedScope,
            Stages = stages
        };
}
