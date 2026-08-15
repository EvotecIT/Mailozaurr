using MimeKit;
using System.Collections;

namespace Mailozaurr;

public partial class ClientSmtp {
    private void AddAddressesToMessage(MimeMessage message) {
        var fromAddresses = From != null ? ConvertToMailboxAddress(From).ToList() : new List<MailboxAddress>();
        if (fromAddresses.Any()) {
            LoggingMessages.Logger.WriteVerbose("Adding from address to message: {0}", fromAddresses.First());
            message.From.Add(fromAddresses.First());
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (To != null && To.Any()) {
            message.To.AddRange(ConvertToMailboxAddressesUnique(To, seen));
        }

        if (Cc != null && Cc.Any()) {
            message.Cc.AddRange(ConvertToMailboxAddressesUnique(Cc, seen));
        }

        if (Bcc != null && Bcc.Any()) {
            message.Bcc.AddRange(ConvertToMailboxAddressesUnique(Bcc, seen));
        }

        if (ReplyTo != null) {
            var replyToAddresses = ConvertToMailboxAddress(ReplyTo).ToList();
            if (replyToAddresses.Any()) {
                message.ReplyTo.Add(replyToAddresses.First());
            }
        }
    }

    private IEnumerable<MailboxAddress> ConvertToMailboxAddressesUnique(IEnumerable<object>? inputs, HashSet<string> seen) {
        if (inputs == null) {
            yield break;
        }

        foreach (var input in inputs) {
            foreach (var address in ConvertToMailboxAddress(input)) {
                var lowered = address.Address.ToLowerInvariant();
                if (seen.Add(lowered)) {
                    yield return address;
                }
            }
        }
    }

    private IEnumerable<MailboxAddress> ConvertToMailboxAddress(object input) {
        switch (input) {
            case string str:
                foreach (var address in ConvertStringToMailboxAddresses(str)) {
                    yield return address;
                }

                break;
            case IDictionary dict:
                foreach (var address in ConvertDictionaryToMailboxAddresses(dict)) {
                    yield return address;
                }

                break;
            case MailboxAddress mailbox:
                yield return mailbox;
                break;
            case IEnumerable<object> list:
                foreach (var address in ConvertListToMailboxAddresses(list)) {
                    yield return address;
                }

                break;
            default:
                throw new ArgumentException($"Invalid input type for ConvertToMailboxAddress: {input}");
        }
    }

    private IEnumerable<MailboxAddress> ConvertStringToMailboxAddresses(string value) {
        MailboxAddress mailbox;
        try {
            mailbox = MailboxAddress.Parse(value);
        } catch (Exception ex) {
            throw new FormatException($"Invalid mailbox address '{value}'.", ex);
        }

        if (value.Contains('<') || value.Contains('>')) {
            yield return mailbox;
        } else {
            yield return new MailboxAddress(string.Empty, mailbox.Address);
        }
    }

    private IEnumerable<MailboxAddress> ConvertDictionaryToMailboxAddresses(IDictionary dict) {
        if (dict.Contains("Name") && dict.Contains("Email")) {
            var name = Convert.ToString(dict["Name"]) ?? string.Empty;
            var email = Convert.ToString(dict["Email"]);
            if (!string.IsNullOrWhiteSpace(email)) {
                yield return new MailboxAddress(name, email);
            }
        }
    }

    private IEnumerable<MailboxAddress> ConvertListToMailboxAddresses(IEnumerable<object> list) {
        foreach (var item in list) {
            foreach (var address in ConvertToMailboxAddress(item)) {
                yield return address;
            }
        }
    }
}