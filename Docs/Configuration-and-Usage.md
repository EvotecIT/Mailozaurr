# Mailozaurr Configuration and Usage

This document collects the practical configuration model for Mailozaurr across its reusable application layer, the `mailozaurr` executable, and MCP usage.

It is meant to answer:

- how Mailozaurr models accounts and providers
- what settings and secrets belong to a profile
- which providers support which kinds of operations
- how to configure CLI storage and run common flows
- where PowerShell still fits today

## Core idea: profiles

Mailozaurr uses reusable `MailProfile` definitions for mailbox and send configuration.

A profile contains:

- `Id`: stable identifier such as `work-imap` or `alerts-smtp`
- `DisplayName`: human-friendly name
- `Kind`: provider/technology such as `imap`, `graph`, `gmail`, or `smtp`
- `DefaultSender`: default sender for send-capable profiles
- `DefaultMailbox`: default mailbox or principal for read-capable profiles
- `Settings`: non-secret provider settings
- secrets stored separately from the profile document

The shared model is implemented in:

- [MailProfile.cs](../Sources/Mailozaurr/MailProfile.cs)
- [MailProfileKind.cs](../Sources/Mailozaurr/MailProfileKind.cs)
- [MailProfileSettingsKeys.cs](../Sources/Mailozaurr/MailProfileSettingsKeys.cs)
- [MailSecretNames.cs](../Sources/Mailozaurr/MailSecretNames.cs)

## Supported profile kinds

Current shared profile kinds are:

- `imap`
- `pop3`
- `graph`
- `gmail`
- `smtp`
- `sendgrid`
- `mailgun`
- `ses`

## Capability model

Mailozaurr does not pretend every provider supports the same operations.

Default capabilities are defined in [MailCapabilityCatalog.cs](../Sources/Mailozaurr/MailCapabilityCatalog.cs).

In practice:

| Kind | Read/Search | Folders | Move/Mark/Delete | Send | Notes |
|---|---|---|---|---|---|
| `imap` | Yes | Yes | Yes | No | strong mailbox model |
| `pop3` | Yes | Virtual `INBOX` only | No normalized actions | No | UIDL-backed ids with content-hash fallback |
| `graph` | Yes | Yes | Yes | Yes | also supports rules/events/permissions |
| `gmail` | Yes | Yes | Yes | Yes | Gmail-specific threads/labels |
| `smtp` | No | No | No | Yes | send only |
| `sendgrid` | No | No | No | Yes | send only |
| `mailgun` | No | No | No | Yes | send only |
| `ses` | No | No | No | Yes | send only |

## Common settings and secrets

Common non-secret setting keys include:

- `server`
- `port`
- `userName`
- `folder`
- `mailbox`
- `clientId`
- `tenantId`
- `certificatePath`
- `redirectUri`
- `authFlow`
- `loginHint`
- `tokenExpiresOn`
- `authMode`
- `secureSocketOptions`
- `useSsl`

Common secret names include:

- `password`
- `clientSecret`
- `accessToken`
- `refreshToken`
- `certificatePassword`

## Minimum provider guidance

The shared validator lives in [MailProfileValidator.cs](../Sources/Mailozaurr/MailProfileValidator.cs).

The practical minimum shape by provider is:

### IMAP / POP3 / SMTP

Usually define:

- `kind`
- `server`
- optional `port`
- optional `userName`
- `password` secret when using username/password auth

Recommended:

- `defaultMailbox` for read profiles
- `defaultSender` for send profiles

### Graph

Usually define:

- `kind=graph`
- mailbox through `defaultMailbox` or `mailbox`

Then choose one auth story:

- interactive login and saved tokens
- access token
- `clientId` + `tenantId` + `clientSecret`
- `clientId` + `tenantId` + `certificatePath`

### Gmail

Usually define:

- `kind=gmail`
- mailbox through `defaultMailbox` or `mailbox`

Then choose one auth story:

- interactive login and saved tokens
- access token
- `clientId` + `clientSecret` + `refreshToken`

### SendGrid / Mailgun / SES

These are send-only profiles. The exact provider-specific settings are still best treated as provider-specific recipes, but they fit the same profile model and shared send surface.

## Storage locations

By default, the application layer stores reusable state under a `Mailozaurr` directory inside local application data.

The path resolver is implemented in [MailApplicationPaths.cs](../Sources/Mailozaurr/MailApplicationPaths.cs).

Default subdirectories are:

- `Profiles`
- `Secrets`
- `Drafts`
- `ActionPlanBatches`

Environment variable overrides:

- `MAILOZAURR_PROFILE_DIRECTORY`
- `MAILOZAURR_SECRET_DIRECTORY`
- `MAILOZAURR_DRAFT_DIRECTORY`
- `MAILOZAURR_ACTION_PLAN_DIRECTORY`

The CLI also supports per-run overrides:

- `--profiles-dir`
- `--secrets-dir`
- `--drafts-dir`
- `--plan-batches-dir`

## CLI usage overview

The executable is built from [Mailozaurr.Cli](../Sources/Mailozaurr.Cli). It is a thin command and MCP surface over reusable workflows in the public `Mailozaurr` assembly. Until the CLI package is publicly released, run it from this checkout or from a locally built PowerForge artifact.

To inspect current commands:

```powershell
dotnet run --project Sources/Mailozaurr.Cli -- --help
```

Main command groups:

- `profile ...`
- `draft ...`
- `send ...`
- `queue ...`
- `mail ...`
- `mcp serve`

Most commands support `--json`, which is the preferred mode for automation.

### Orphaned profile secrets

Inspect the secret store after profiles have been moved, restored, or edited outside the application:

```powershell
mailozaurr profile inspect-orphan-secrets --json
```

The report contains structured secret sets whose profile no longer exists. It also lists ambiguous legacy keys separately without exposing their protected values. Cleanup removes only the structured orphan sets; ambiguous legacy keys are retained because Mailozaurr cannot prove where the profile identifier ends and the secret name begins.

The built-in file stores coordinate cleanup with profile writes so a profile created in another process cannot lose its secrets. Applications that replace the stores should implement `IMailProfileMaintenanceCoordinator` together with `IMailProfileSecretMaintenanceStore`, or inject an `IMailProfileSecretMaintenanceService` that provides equivalent coordination.

```powershell
mailozaurr profile cleanup-orphan-secrets --json
```

## Common CLI recipes

### Generic IMAP profile

```powershell
mailozaurr profile create --profile work-imap --kind imap --name "Work IMAP" `
  --default-mailbox user@example.com `
  --setting server=imap.example.com `
  --setting port=993 `
  --setting userName=user@example.com `
  --json

$env:MAILOZAURR_IMAP_PASSWORD = '<password>'
mailozaurr profile set-secret --profile work-imap --name password `
  --value-env MAILOZAURR_IMAP_PASSWORD --json
Remove-Item Env:MAILOZAURR_IMAP_PASSWORD

mailozaurr profile test --profile work-imap --scope mailbox --json
```

### Generic POP3 profile

```powershell
mailozaurr profile create --profile archive-pop3 --kind pop3 --name "Archive POP3" `
  --default-mailbox user@example.com `
  --setting server=pop.example.com `
  --setting port=995 `
  --setting userName=user@example.com `
  --json

$env:MAILOZAURR_POP3_PASSWORD = '<password>'
mailozaurr profile set-secret --profile archive-pop3 --name password `
  --value-env MAILOZAURR_POP3_PASSWORD --json
Remove-Item Env:MAILOZAURR_POP3_PASSWORD

mailozaurr profile test --profile archive-pop3 --scope mailbox --json
mailozaurr mail folders --profile archive-pop3 --json
mailozaurr mail search --profile archive-pop3 --query Invoice --json
```

Normalized POP3 operations expose one virtual `INBOX`. Message ids prefer the server UIDL as `uid:<value>` and fall back to `hash:<sha256>:<occurrence>` when UIDL is unavailable. The fallback verifies message content and disambiguates byte-identical entries by their oldest-first occurrence while those entries coexist; only UIDL provides a server-owned persistent identity across mailbox mutations. Use the returned id unchanged with `mail get` and attachment commands.

### Generic SMTP profile

```powershell
mailozaurr profile create --profile alerts-smtp --kind smtp --name "Alerts SMTP" `
  --default-sender alerts@example.com `
  --setting server=smtp.example.com `
  --setting port=587 `
  --setting userName=alerts@example.com `
  --setting useSsl=true `
  --json

$env:MAILOZAURR_SMTP_PASSWORD = '<password>'
mailozaurr profile set-secret --profile alerts-smtp --name password `
  --value-env MAILOZAURR_SMTP_PASSWORD --json
Remove-Item Env:MAILOZAURR_SMTP_PASSWORD

mailozaurr profile test --profile alerts-smtp --scope send --json
```

`profile test --json` keeps the existing summary fields and also returns ordered `Stages`. Each stage identifies the observable profile, session, mailbox, provider-probe, or send-preflight phase, its duration, target, and failure code. Mailozaurr reports the combined session operation exposed by each provider factory; it does not invent separate DNS, TCP, TLS, or authentication timings when those boundaries are not observable.

### Microsoft Graph profile

```powershell
$env:MAILOZAURR_GRAPH_CLIENT_SECRET = '<client-secret>'
mailozaurr profile graph-bootstrap --profile work-graph --name "Work Graph" `
  --mailbox user@example.com `
  --client-id <app-id> `
  --tenant-id <tenant-id> `
  --client-secret-env MAILOZAURR_GRAPH_CLIENT_SECRET `
  --json
Remove-Item Env:MAILOZAURR_GRAPH_CLIENT_SECRET

mailozaurr profile auth-status --profile work-graph --json
mailozaurr profile test --profile work-graph --scope mailbox --json
```

### Gmail profile

```powershell
$env:MAILOZAURR_GMAIL_CLIENT_SECRET = '<client-secret>'
$env:MAILOZAURR_GMAIL_REFRESH_TOKEN = '<refresh-token>'
mailozaurr profile gmail-bootstrap --profile personal-gmail --name "Personal Gmail" `
  --mailbox user@gmail.com `
  --client-id <client-id> `
  --client-secret-env MAILOZAURR_GMAIL_CLIENT_SECRET `
  --refresh-token-env MAILOZAURR_GMAIL_REFRESH_TOKEN `
  --json
Remove-Item Env:MAILOZAURR_GMAIL_CLIENT_SECRET
Remove-Item Env:MAILOZAURR_GMAIL_REFRESH_TOKEN

mailozaurr profile auth-status --profile personal-gmail --json
mailozaurr profile test --profile personal-gmail --scope mailbox --json
```

### Search and retrieve mail

```powershell
mailozaurr mail folders --profile work-imap --compact --json
mailozaurr mail search --profile work-imap --folder Inbox --query Invoice --compact --json
mailozaurr mail get --profile work-imap --folder Inbox --message-id 123 --compact --json
mailozaurr mail attachments --profile work-imap --folder Inbox --message-id 123 --json
mailozaurr mail save-attachments --profile work-imap --folder Inbox --message-id 123 --path C:\Temp\Attachments --json
```

When `--path` names a directory, saved files keep a readable prefix and add a deterministic SHA-256 identity suffix from the exact provider filename plus the profile, mailbox, folder, message, and provider/per-part identity. This prevents distinct remote names, repeated same-name parts, and cross-message batch saves from overwriting one another after path removal, character replacement, or case folding. An explicit file path is used unchanged.

### Save drafts and queue sends

```powershell
mailozaurr draft save --draft weekly-update --name "Weekly update" --profile alerts-smtp `
  --to team@example.com --subject "Weekly update" --text "Status attached." --json

mailozaurr send --draft weekly-update --json
mailozaurr queue list --compact --json
mailozaurr queue process --json
```

Send commands attempt immediate delivery by default. Use `--queue-on-failure` to persist a message only when that immediate attempt fails.

### Preview before destructive actions

```powershell
mailozaurr mail preview-delete --profile work-imap --folder Inbox --message-id 123 --json
mailozaurr mail delete --profile work-imap --folder Inbox --message-id 123 --confirm-token <token> --json
```

## MCP usage overview

The MCP server is hosted by the same executable:

```powershell
mailozaurr mcp serve
```

The MCP tool surface is implemented in [MailMcpTools.cs](../Sources/Mailozaurr.Cli/Mcp/MailMcpTools.cs).

Current tool areas include:

- profile listing, save, delete, bootstrap, login, auth status, and live tests
- folder listing and folder-alias discovery
- search, get, batch get, and attachment save
- drafts, send, and queue operations
- preview and execution for read/flag/move/archive/trash/delete actions
- action-plan import/export, execution, and stored batch management

A minimal MCP client entry looks like:

```json
{
  "command": "mailozaurr",
  "args": ["mcp", "serve"]
}
```

## PowerShell usage today

PowerShell remains a first-class surface, but it does not use the same profile-oriented headless workflow yet in the same way as the CLI and MCP layers.

Today the best PowerShell references are:

- [README.MD](../README.MD) for module usage and examples
- [OAuthFlows.md](./OAuthFlows.md) for OAuth flows
- [PGP.md](./PGP.md) for PGP support
- the scripts under [Examples](../Examples)

## Recipes and provider-specific guidance

Because Mailozaurr supports many providers and auth stories, examples matter.

When adding a new provider recipe:

- document the minimum profile shape
- show both settings and secrets
- include a live verification command such as `profile test`
- keep reusable logic in shared layers and keep wrapper-specific steps in wrapper docs

If a feature is reusable across CLI, MCP, GUI, or PowerShell, it should follow the placement rules in [Platform-Architecture.md](./Platform-Architecture.md).
