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

- [MailProfile.cs](../Sources/Mailozaurr.Application/MailProfile.cs)
- [MailProfileKind.cs](../Sources/Mailozaurr.Application/MailProfileKind.cs)
- [MailProfileSettingsKeys.cs](../Sources/Mailozaurr.Application/MailProfileSettingsKeys.cs)
- [MailSecretNames.cs](../Sources/Mailozaurr.Application/MailSecretNames.cs)

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

Default capabilities are defined in [MailCapabilityCatalog.cs](../Sources/Mailozaurr.Application/MailCapabilityCatalog.cs).

In practice:

| Kind | Read/Search | Folders | Move/Mark/Delete | Send | Notes |
|---|---|---|---|---|---|
| `imap` | Yes | Yes | Yes | No | strong mailbox model |
| `pop3` | Yes | No real folder model | Delete only style workflows | No | limited mailbox model |
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

The shared validator lives in [MailProfileValidator.cs](../Sources/Mailozaurr.Application/MailProfileValidator.cs).

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

The path resolver is implemented in [MailApplicationPaths.cs](../Sources/Mailozaurr.Application/MailApplicationPaths.cs).

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

The executable is built from [Mailozaurr.Cli](../Sources/Mailozaurr.Cli).

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
```

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
