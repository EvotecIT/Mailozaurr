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
                | MailCapability.DeleteMessages,
            MailProfileKind.Graph => MailCapability.ListFolders
                | MailCapability.SearchMessages
                | MailCapability.ReadMessages
                | MailCapability.SaveAttachments
                | MailCapability.MarkMessages
                | MailCapability.MoveMessages
                | MailCapability.DeleteMessages
                | MailCapability.SendMessages,
            MailProfileKind.Gmail => MailCapability.ListFolders
                | MailCapability.SearchMessages
                | MailCapability.ReadMessages
                | MailCapability.SaveAttachments
                | MailCapability.MarkMessages
                | MailCapability.MoveMessages
                | MailCapability.DeleteMessages
                | MailCapability.SendMessages,
            MailProfileKind.Smtp => MailCapability.SendMessages,
            _ => MailCapability.None,
        };

        return new ProfileCapabilities(kind, capabilities);
    }

    internal static IReadOnlyDictionary<MailProfileKind, MailCapability> ForRegisteredHandlers(
        IEnumerable<IMailReadHandler> readHandlers,
        IEnumerable<IMailMessageActionHandler> messageActionHandlers,
        IEnumerable<IMailSendHandler> sendHandlers) {
        var result = new Dictionary<MailProfileKind, MailCapability>();
        Add(result, readHandlers.Select(handler => handler.Kind),
            MailCapability.ListFolders | MailCapability.SearchMessages |
            MailCapability.ReadMessages | MailCapability.SaveAttachments);
        Add(result, messageActionHandlers.Select(handler => handler.Kind),
            MailCapability.MarkMessages | MailCapability.MoveMessages | MailCapability.DeleteMessages);
        Add(result, sendHandlers.Select(handler => handler.Kind), MailCapability.SendMessages);
        return result;
    }

    private static void Add(IDictionary<MailProfileKind, MailCapability> destination,
        IEnumerable<MailProfileKind> kinds, MailCapability capabilities) {
        foreach (MailProfileKind kind in kinds.Distinct()) {
            destination.TryGetValue(kind, out MailCapability existing);
            destination[kind] = existing | capabilities;
        }
    }
}
