using MimeKit;

namespace Mailozaurr.PowerShell;

internal static class PowerShellMimeMessageResolver {
    public static MimeMessage? Resolve(object? input) {
        input = input is PSObject psObject ? psObject.BaseObject : input;

        return input switch {
            MimeMessage message => message,
            ImapMessageInfo info => info.Raw.Message,
            ImapEmailMessage imap => imap.Message,
            Pop3MessageInfo info => info.Raw.Message,
            Pop3EmailMessage pop => pop.Message,
            GraphEmailMessage graph => graph.Message,
            _ => null
        };
    }
}