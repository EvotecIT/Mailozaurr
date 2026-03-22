namespace Mailozaurr.Application;

/// <summary>
/// Provides the default normalized capability map for each profile kind.
/// </summary>
public static class MailCapabilityCatalog {
    /// <summary>
    /// Returns the default capabilities for a given profile kind.
    /// </summary>
    public static ProfileCapabilities For(MailProfileKind kind) {
        var capabilities = kind switch {
            MailProfileKind.Imap => MailCapability.ListFolders
                | MailCapability.SearchMessages
                | MailCapability.ReadMessages
                | MailCapability.SaveAttachments
                | MailCapability.MarkMessages
                | MailCapability.MoveMessages
                | MailCapability.DeleteMessages
                | MailCapability.WaitForMessages,
            MailProfileKind.Pop3 => MailCapability.SearchMessages
                | MailCapability.ReadMessages
                | MailCapability.SaveAttachments
                | MailCapability.DeleteMessages
                | MailCapability.WaitForMessages,
            MailProfileKind.Graph => MailCapability.ListFolders
                | MailCapability.SearchMessages
                | MailCapability.ReadMessages
                | MailCapability.SaveAttachments
                | MailCapability.MarkMessages
                | MailCapability.MoveMessages
                | MailCapability.DeleteMessages
                | MailCapability.SendMessages
                | MailCapability.WaitForMessages
                | MailCapability.ManageRules
                | MailCapability.ManageEvents
                | MailCapability.ManagePermissions,
            MailProfileKind.Gmail => MailCapability.ListFolders
                | MailCapability.SearchMessages
                | MailCapability.ReadMessages
                | MailCapability.SaveAttachments
                | MailCapability.DeleteMessages
                | MailCapability.SendMessages
                | MailCapability.UseThreads
                | MailCapability.UseLabels,
            MailProfileKind.Smtp => MailCapability.SendMessages,
            MailProfileKind.SendGrid => MailCapability.SendMessages,
            MailProfileKind.Mailgun => MailCapability.SendMessages,
            MailProfileKind.Ses => MailCapability.SendMessages,
            _ => MailCapability.None,
        };

        return new ProfileCapabilities(kind, capabilities);
    }
}
