using System.CommandLine;

namespace Mailozaurr.Cli;

internal static partial class CliCommandModel {
    private static Command CreateProfileCommand() => Group(
        "profile",
        "Manage provider profiles, authentication, capabilities, and protected secrets.",
        Leaf("list", "List configured profiles.",
            optional: ["summary", "compact", "kind", "ready-only", "can-read", "can-send", "default-only", "sort", "desc"]),
        Leaf("create", "Create or replace a provider profile.",
            required: ["profile", "kind", "name"],
            optional: ["description", "default-sender", "default-mailbox", "is-default", "setting"]),
        Leaf("graph-bootstrap", "Create a Microsoft Graph profile using secret-safe inputs.",
            required: ["profile", "name", "mailbox"],
            optional: [
                "description", "default-sender", "is-default", "client-id", "tenant-id",
                "client-secret-env", "client-secret-stdin", "client-secret-ref",
                "access-token-env", "access-token-stdin", "access-token-ref",
                "certificate-path", "certificate-password-env", "certificate-password-stdin",
                "certificate-password-ref", "allow-cross-profile-secret-ref"
            ]),
        Leaf("gmail-bootstrap", "Create a Gmail profile using secret-safe inputs.",
            required: ["profile", "name"],
            optional: [
                "mailbox", "description", "default-sender", "is-default", "client-id",
                "client-secret-env", "client-secret-stdin", "client-secret-ref",
                "refresh-token-env", "refresh-token-stdin", "refresh-token-ref",
                "access-token-env", "access-token-stdin", "access-token-ref",
                "allow-cross-profile-secret-ref"
            ]),
        Leaf("graph-login", "Authenticate an existing Microsoft Graph profile interactively.",
            required: ["profile"],
            optional: ["login", "mailbox", "client-id", "tenant-id", "redirect-uri", "scope"]),
        Leaf("gmail-login", "Authenticate an existing Gmail profile interactively.",
            required: ["profile"],
            optional: [
                "mailbox", "client-id", "client-secret-env", "client-secret-stdin",
                "client-secret-ref", "allow-cross-profile-secret-ref", "scope"
            ]),
        Leaf("refresh-auth", "Refresh saved authentication for a profile.", required: ["profile"]),
        Leaf("auth-status", "Show persisted authentication status.", required: ["profile"]),
        Leaf("test", "Test profile authentication, mailbox, or send readiness with timed phase evidence.",
            required: ["profile"], optional: ["scope"]),
        Leaf("summary", "Show the normalized profile summary.",
            required: ["profile"], optional: ["compact"]),
        Leaf("capabilities", "Show capabilities available in the current composition.",
            required: ["profile"]),
        Leaf("show", "Show a profile without exposing secret values.", required: ["profile"]),
        Leaf("validate", "Validate a profile contract.", required: ["profile"]),
        Leaf("doctor", "Diagnose profile readiness.", required: ["profile"]),
        Leaf("delete", "Delete a profile and its owned secrets.", required: ["profile"]),
        Leaf("set-default", "Set the default profile.", required: ["profile"]),
        Leaf("set-secret", "Store or copy a protected secret without a command-line value.",
            required: ["profile", "name"],
            optional: ["value-env", "value-stdin", "value-ref", "allow-cross-profile-secret-ref"]),
        Leaf("remove-secret", "Remove a protected profile secret.",
            required: ["profile", "name"]),
        Leaf("inspect-orphan-secrets", "Inspect structured orphan-secret sets without exposing values."),
        Leaf("cleanup-orphan-secrets", "Remove structured orphan-secret sets safely."));
}
