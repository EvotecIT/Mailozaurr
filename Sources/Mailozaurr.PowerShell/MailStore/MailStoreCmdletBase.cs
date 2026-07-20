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
