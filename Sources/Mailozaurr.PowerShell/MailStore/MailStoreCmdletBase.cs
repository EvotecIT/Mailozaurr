using OfficeIMO.Email;
using OfficeIMO.Email.AddressBook;
using OfficeIMO.Email.Data;
using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>Shared PowerShell-only adaptation for OfficeIMO.Email store workflows.</summary>
public abstract class MailStoreCmdletBase : AsyncPSCmdlet {
    /// <summary>Resolves either a unified email-data result or a native store session.</summary>
    protected static EmailStoreSession GetStoreSession(object? inputObject) {
        object? value = inputObject is PSObject psObject ? psObject.BaseObject : inputObject;
        return value switch {
            EmailStoreSession session => session,
            EmailDataOpenResult { Store: not null } result => result.Store,
            EmailDataOpenResult result => throw new PSArgumentException(
                $"The imported artifact is '{result.Kind}', not a mailbox store."),
            null => throw new PSArgumentNullException(nameof(inputObject)),
            _ => throw new PSArgumentException(
                "InputObject must be an EmailDataOpenResult containing a store or an EmailStoreSession.")
        };
    }

    /// <summary>Resolves an imported email or its native document.</summary>
    protected static EmailDocument GetEmailDocument(object? inputObject) {
        object? value = inputObject is PSObject psObject ? psObject.BaseObject : inputObject;
        return value switch {
            EmailDocument document => document,
            EmailDataOpenResult { EmailDocument: not null } result => result.EmailDocument!,
            EmailDataOpenResult result => throw new PSArgumentException(
                $"The imported artifact is '{result.Kind}', not an email document."),
            null => throw new PSArgumentNullException(nameof(inputObject)),
            _ => throw new PSArgumentException(
                "InputObject must be an EmailDocument or an Import-MailData result containing one.")
        };
    }

    /// <summary>Resolves an imported Offline Address Book or its native session.</summary>
    protected static OfflineAddressBookSession GetAddressBookSession(object? inputObject) {
        object? value = inputObject is PSObject psObject ? psObject.BaseObject : inputObject;
        return value switch {
            OfflineAddressBookSession session => session,
            EmailDataOpenResult { AddressBook: not null } result => result.AddressBook!,
            EmailDataOpenResult result => throw new PSArgumentException(
                $"The imported artifact is '{result.Kind}', not an Offline Address Book."),
            null => throw new PSArgumentNullException(nameof(inputObject)),
            _ => throw new PSArgumentException(
                "InputObject must be an OfflineAddressBookSession or an Import-MailData result containing one.")
        };
    }

    /// <summary>Writes native OfficeIMO store diagnostics without replacing the structured report.</summary>
    protected void WriteStoreDiagnostics(IEnumerable<EmailStoreDiagnostic> diagnostics, object? target) {
        foreach (EmailStoreDiagnostic diagnostic in diagnostics) {
            string message = $"{diagnostic.Code}: {diagnostic.Message}";
            if (diagnostic.Severity == EmailStoreDiagnosticSeverity.Error) {
                WriteError(new ErrorRecord(
                    new InvalidDataException(message),
                    diagnostic.Code,
                    ErrorCategory.InvalidData,
                    target));
            } else if (diagnostic.Severity == EmailStoreDiagnosticSeverity.Warning) {
                WriteWarning(message);
            } else {
                WriteVerbose(message);
            }
        }
    }
}
