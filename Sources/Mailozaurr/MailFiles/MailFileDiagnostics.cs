using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Normalizes OfficeIMO diagnostic failures at Mailozaurr API boundaries.</summary>
internal static class MailFileDiagnostics {
    internal static bool TryGetError(
        IReadOnlyList<EmailDiagnostic> diagnostics,
        string context,
        out string? error) {
        foreach (EmailDiagnostic diagnostic in diagnostics) {
            if (diagnostic.Severity != EmailDiagnosticSeverity.Error) continue;
            error = string.Concat(context, ": ", diagnostic.Code, ": ", diagnostic.Message);
            return true;
        }

        error = null;
        return false;
    }

    internal static void ThrowIfErrors(IReadOnlyList<EmailDiagnostic> diagnostics, string context) {
        if (TryGetError(diagnostics, context, out string? error)) {
            throw new InvalidDataException(error);
        }
    }
}
