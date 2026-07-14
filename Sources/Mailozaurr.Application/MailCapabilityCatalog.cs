namespace Mailozaurr.Application;

/// <summary>
/// Provides the default normalized capability map for each profile kind.
/// </summary>
public static class MailCapabilityCatalog {
    private static readonly MailProfileKind[] KnownProfileKinds = {
        MailProfileKind.Imap,
        MailProfileKind.Pop3,
        MailProfileKind.Graph,
        MailProfileKind.Gmail,
        MailProfileKind.Smtp,
        MailProfileKind.SendGrid,
        MailProfileKind.Mailgun,
        MailProfileKind.Ses
    };
    private const MailCapability ReadServiceCapabilities = MailCapability.ListFolders
        | MailCapability.SearchMessages
        | MailCapability.ReadMessages
        | MailCapability.SaveAttachments;
    private const MailCapability MessageActionServiceCapabilities = MailCapability.MarkMessages
        | MailCapability.MoveMessages
        | MailCapability.DeleteMessages;

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
        IEnumerable<IMailSendHandler> sendHandlers,
        bool hasReadServiceOverride = false,
        bool hasMessageActionServiceOverride = false,
        bool hasSendServiceOverride = false) {
        var result = new Dictionary<MailProfileKind, MailCapability>();
        Add(result, readHandlers.Select(handler => handler.Kind), ReadServiceCapabilities);
        Add(result, messageActionHandlers.Select(handler => handler.Kind), MessageActionServiceCapabilities);
        Add(result, sendHandlers.Select(handler => handler.Kind), MailCapability.SendMessages);
        AddServiceOverrideCapabilities(result, hasReadServiceOverride, ReadServiceCapabilities);
        AddServiceOverrideCapabilities(
            result,
            hasMessageActionServiceOverride,
            MessageActionServiceCapabilities);
        AddServiceOverrideCapabilities(result, hasSendServiceOverride, MailCapability.SendMessages);
        return result;
    }

    private static void AddServiceOverrideCapabilities(
        IDictionary<MailProfileKind, MailCapability> destination,
        bool hasServiceOverride,
        MailCapability capabilities) {
        if (!hasServiceOverride) return;
        foreach (MailProfileKind kind in KnownProfileKinds) {
            MailCapability available = For(kind).Capabilities & capabilities;
            if (available == MailCapability.None) continue;
            destination.TryGetValue(kind, out MailCapability existing);
            destination[kind] = existing | available;
        }
    }

    private static void Add(IDictionary<MailProfileKind, MailCapability> destination,
        IEnumerable<MailProfileKind> kinds, MailCapability capabilities) {
        foreach (MailProfileKind kind in kinds.Distinct()) {
            destination.TryGetValue(kind, out MailCapability existing);
            destination[kind] = existing | capabilities;
        }
    }
}
