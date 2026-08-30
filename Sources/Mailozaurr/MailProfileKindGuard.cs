namespace Mailozaurr;

internal static class MailProfileKindGuard {
    internal static bool IsChanged(MailProfile existing, MailProfile candidate) =>
        existing.Kind != candidate.Kind;

    internal static void EnsureUnchanged(MailProfile existing, MailProfile candidate) {
        if (IsChanged(existing, candidate)) {
            throw new MailProfileKindChangeException(existing.Kind, candidate.Kind);
        }
    }
}

internal sealed class MailProfileKindChangeException : InvalidOperationException {
    internal MailProfileKindChangeException(MailProfileKind existingKind, MailProfileKind requestedKind)
        : base($"Changing an existing profile provider from '{existingKind}' to '{requestedKind}' is not allowed. Delete and recreate the profile so provider secrets cannot be reused.") {
        ExistingKind = existingKind;
        RequestedKind = requestedKind;
    }

    internal MailProfileKind ExistingKind { get; }
    internal MailProfileKind RequestedKind { get; }
}
