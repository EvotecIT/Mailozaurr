namespace Mailozaurr.Application;

/// <summary>
/// Configures the default Mailozaurr application composition.
/// </summary>
public sealed class MailApplicationOptions {
    /// <summary>Options used for the default profile store.</summary>
    public MailProfileStoreOptions ProfileStore { get; set; } = new();

    /// <summary>Options used for the default secret store.</summary>
    public MailSecretStoreOptions SecretStore { get; set; } = new();

    /// <summary>Options used for the default draft store.</summary>
    public MailDraftStoreOptions DraftStore { get; set; } = new();

    /// <summary>Options used for the default reusable action plan batch store.</summary>
    public MailMessageActionPlanBatchStoreOptions ActionPlanBatchStore { get; set; } = new();

    /// <summary>Options used for the default pending-message repository.</summary>
    public PendingMessageRepositoryOptions PendingMessageStore { get; set; } = new();

    /// <summary>Whether the built-in IMAP read handler should be registered.</summary>
    public bool EnableImapReadHandler { get; set; } = true;

    /// <summary>Whether the built-in Graph read handler should be registered.</summary>
    public bool EnableGraphReadHandler { get; set; } = true;

    /// <summary>Whether the built-in Graph send handler should be registered.</summary>
    public bool EnableGraphSendHandler { get; set; } = true;

    /// <summary>Whether the built-in Gmail read handler should be registered.</summary>
    public bool EnableGmailReadHandler { get; set; } = true;

    /// <summary>Whether the built-in Gmail send handler should be registered.</summary>
    public bool EnableGmailSendHandler { get; set; } = true;

    /// <summary>Whether the built-in SMTP send handler should be registered.</summary>
    public bool EnableSmtpSendHandler { get; set; } = true;
}