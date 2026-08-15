namespace Mailozaurr.Hosting;

/// <summary>
/// Provides the default normalized capability map for each profile kind.
/// </summary>
public static class MailCapabilityCatalog {
    private static readonly MailProfileKind[] ReadServiceProfileKinds = {
        MailProfileKind.Imap,
        MailProfileKind.Pop3,
        MailProfileKind.Graph,
        MailProfileKind.Gmail
    };
    private static readonly MailProfileKind[] MessageActionServiceProfileKinds = {
        MailProfileKind.Imap,
        MailProfileKind.Graph,
        MailProfileKind.Gmail
    };
    private static readonly MailProfileKind[] SendServiceProfileKinds = {
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
            MailProfileKind.SendGrid => MailCapability.SendMessages,
            MailProfileKind.Mailgun => MailCapability.SendMessages,
            MailProfileKind.Ses => MailCapability.SendMessages,
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
        AddServiceOverrideCapabilities(
            result,
            hasReadServiceOverride,
            ReadServiceProfileKinds,
            ReadServiceCapabilities);
        AddServiceOverrideCapabilities(
            result,
            hasMessageActionServiceOverride,
            MessageActionServiceProfileKinds,
            MessageActionServiceCapabilities);
        AddServiceOverrideCapabilities(
            result,
            hasSendServiceOverride,
            SendServiceProfileKinds,
            MailCapability.SendMessages);
        return result;
    }

    private static void AddServiceOverrideCapabilities(
        IDictionary<MailProfileKind, MailCapability> destination,
        bool hasServiceOverride,
        IEnumerable<MailProfileKind> kinds,
        MailCapability capabilities) {
        if (!hasServiceOverride) return;
        Add(destination, kinds, capabilities);
    }

    private static void Add(IDictionary<MailProfileKind, MailCapability> destination,
        IEnumerable<MailProfileKind> kinds, MailCapability capabilities) {
        foreach (MailProfileKind kind in kinds.Distinct()) {
            destination.TryGetValue(kind, out MailCapability existing);
            destination[kind] = existing | capabilities;
        }
    }
}
