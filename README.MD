# Mailozaurr - Modern Email Toolkit for .NET and PowerShell

Mailozaurr is available as a NuGet package and as a PowerShell module from PSGallery.

NuGet Package

[![nuget downloads](https://img.shields.io/nuget/dt/Mailozaurr?label=nuget%20downloads)](https://www.nuget.org/packages/Mailozaurr)
[![nuget version](https://img.shields.io/nuget/v/Mailozaurr)](https://www.nuget.org/packages/Mailozaurr)

PowerShell Module

[![powershell gallery version](https://img.shields.io/powershellgallery/v/Mailozaurr.svg)](https://www.powershellgallery.com/packages/Mailozaurr)
[![powershell gallery preview](https://img.shields.io/powershellgallery/v/Mailozaurr.svg?label=powershell%20gallery%20preview&colorB=yellow&include_prereleases)](https://www.powershellgallery.com/packages/Mailozaurr)
[![powershell gallery platforms](https://img.shields.io/powershellgallery/p/Mailozaurr.svg)](https://www.powershellgallery.com/packages/Mailozaurr)
[![powershell gallery downloads](https://img.shields.io/powershellgallery/dt/Mailozaurr.svg)](https://www.powershellgallery.com/packages/Mailozaurr)

Project Information

[![build status](https://dev.azure.com/evotecpl/Mailozaurr/_apis/build/status/EvotecIT.Mailozaurr)](https://dev.azure.com/evotecpl/Mailozaurr/_build/results?buildId=latest)
[![codecov](https://codecov.io/gh/EvotecIT/Mailozaurr/branch/v2-speedygonzales/graph/badge.svg)](https://codecov.io/gh/EvotecIT/Mailozaurr)
[![top language](https://img.shields.io/github/languages/top/evotecit/Mailozaurr.svg)](https://github.com/EvotecIT/Mailozaurr)
[![license](https://img.shields.io/github/license/EvotecIT/Mailozaurr.svg)](https://github.com/EvotecIT/Mailozaurr)
[![code size](https://img.shields.io/github/languages/code-size/evotecit/Mailozaurr.svg)](https://github.com/EvotecIT/Mailozaurr)

Author and Social

[![Twitter follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Discord](https://img.shields.io/discord/508328927853281280?style=flat-square&label=discord%20chat)](https://evo.yt/discord)

## What it's all about

Mailozaurr provides SMTP, POP3, IMAP, Microsoft Graph mail support, and mail-format tooling for PowerShell and .NET. Underneath it uses MimeKit and MailKit libraries written by Jeffrey Stedfast.

This repository has two branches:
- `v1-legacy`: old module generation, written in PowerShell and still maintained until V2 is finalized.
- `v2-speedygonzales`: current rewrite in C# with newer functionality and better performance.

In V2, some functionality was moved to dedicated projects:
- DNS features moved to [DnsClientX](https://github.com/EvotecIT/DnsClientX).
- SPF, DKIM, DMARC, and MX validation (except download-focused functionality) moved to [DomainDetective](https://github.com/EvotecIT/DomainDetective).

## Core dependencies (V2)

- [MailKit](https://github.com/jstedfast/MailKit) - licensed MIT
- [MimeKit](https://github.com/jstedfast/MimeKit) - licensed MIT
- [OfficeIMO.Email](https://github.com/EvotecIT/OfficeIMO) - licensed MIT

For OAuth2 support, Mailozaurr also bundles:

- [Microsoft.Identity.Client](https://www.nuget.org/packages/Microsoft.Identity.Client/)
- [Google.Apis](https://www.nuget.org/packages/Google.Apis)
- [Google.Apis.Core](https://www.nuget.org/packages/Google.Apis.Core)
- [Google.Apis.Auth](https://www.nuget.org/packages/Google.Apis.Auth)

## EML, MSG, OFT, and TNEF files

Mailozaurr keeps transport, mailbox access, MimeKit message composition, PGP, DKIM, ARC, and outbound S/MIME workflows. OfficeIMO.Email owns local EML, MSG, OFT, and TNEF artifact reading, writing, and projection, including typed Outlook items, RTF-backed bodies, embedded items, retained MAPI properties, and bounded S/MIME verification through OfficeIMO.Security.

PowerShell callers can import EML, MSG, OFT, and TNEF files through the same command. The returned message owns the OfficeIMO read result, so dispose it when the projection is no longer needed:

```powershell
$mail = Import-MailFile './message.msg'
try {
    $mail.Subject
    $mail.OutlookItemKind
    $mail.OfficeDocument.Contact
    $mail.Diagnostics
} finally {
    $mail.Dispose()
}
```

Exporting uses the destination extension, so the same pipeline converts in either direction:

```powershell
Import-MailFile './message.msg' | Export-MailFile './message.eml'
Import-MailFile './message.eml' | Export-MailFile './message.msg'
```

`Export-MailFile` consumes and disposes each input message after an attempted export, including when conversion reports an error. Previewed or rejected operations such as `-WhatIf` or an existing destination without `-Force` leave the message open. Add `-KeepInputOpen` when a retained `$mail` variable must remain usable after export. Add `-Force` to replace an existing destination or `-PassThru` to return its `FileInfo`. Use `-IncludeHeaders` when merged headers are needed, `-ExcludeAttachments` for message metadata only, or `-ExcludeAttachmentContent` to retain attachment names and sizes without loading decoded bytes. EML input also exposes its native `MimeMessage`; MSG input can produce one with `$mail.ToMimeMessage()` when it needs to enter a Mailozaurr cryptography or transport workflow.

The direction-specific conversion commands remain available for scripts that write batches to an output folder:

```powershell
ConvertFrom-EmlToMsg -InputPath './message.eml' -OutputFolder './converted'
ConvertFrom-MsgToEml -InputPath './message.msg' -OutputFolder './converted'
```

The .NET API follows the same load/save shape:

```csharp
using Mailozaurr;

using MailFileMessage mail = MailFileMessage.Load("message.msg");
Console.WriteLine(mail.Subject);

await mail.SaveAsync("message.eml");
```

`MailFileMessage.Load` and `Import-MailFile` fail clearly on error diagnostics. Use `MailFileReader` when custom projection options or recovery from a partially readable artifact are needed; check `message.HasErrors` and `message.Diagnostics` before accepting it. Disposing the message releases any temporary file-backed attachment content retained by OfficeIMO. The older `EmailMessage` conversion methods remain available for compatibility and batch conversion. A separate `Mailozaurr.Msg` package is no longer required.

## Architecture notes

- [Platform Architecture](Docs/Platform-Architecture.md) - layering and reuse rules for future library, PowerShell, CLI, MCP, and GUI work
- [Configuration and Usage](Docs/Configuration-and-Usage.md) - profile model, provider setup guidance, CLI storage, recipes, and MCP usage

## Additional surfaces

Mailozaurr is no longer only a library and PowerShell module. The repository now also contains shared application-layer work and headless surfaces for broader automation scenarios:

- `.NET library` for direct application and service integration
- `PowerShell module` for administrators and automation users
- `mailozaurr` executable for cross-platform CLI workflows
- `mailozaurr mcp serve` for MCP-based integrations

CLI and MCP are intended to sit on top of shared Mailozaurr services instead of duplicating mailbox, draft, queue, send, and message-action behavior independently.

If you want the placement rules and longer-term cross-surface direction, see [Platform Architecture](Docs/Platform-Architecture.md).
If you want practical profile setup and usage guidance for CLI and MCP, see [Configuration and Usage](Docs/Configuration-and-Usage.md).

## CLI and MCP usage

The repository now includes a cross-platform `mailozaurr` executable and an MCP server hosted by that same executable.

The CLI and application packages are built by the repository release pipeline. Until those packages are published, run the CLI from a checkout as shown below.

To see the current command surface from source:

```powershell
dotnet run --project Sources/Mailozaurr.Cli -- --help
```

After building, the executable is available at:

- `Sources/Mailozaurr.Cli/bin/Debug/net8.0/mailozaurr.exe` on Windows
- `Sources/Mailozaurr.Cli/bin/Debug/net8.0/mailozaurr` on macOS/Linux

### CLI command groups

The executable currently supports:

- `profile ...` for profile creation, bootstrap, login, auth refresh, readiness checks, summaries, validation, and secret management
- `draft ...` for saving, listing, exporting, importing, and deleting reusable drafts
- `send ...` for sending from a stored draft, a draft file, or direct command-line fields
- `queue ...` for viewing and processing queued outbound messages
- `mail ...` for folders, folder aliases, search, message retrieval, attachment export, mailbox actions, previews, and reusable action-plan batches
- `mcp serve` for exposing the same shared services as MCP tools over stdio

Most list and inspection commands support `--json`, which makes the executable useful in scripts, CI, and other automation surfaces.

### CLI examples

Create a profile and inspect it:

```powershell
mailozaurr profile create --profile work-imap --kind imap --name "Work IMAP" `
  --setting server=imap.example.com --setting port=993 --json

$env:MAILOZAURR_IMAP_PASSWORD = '<password>'
mailozaurr profile set-secret --profile work-imap --name password `
  --value-env MAILOZAURR_IMAP_PASSWORD --json
Remove-Item Env:MAILOZAURR_IMAP_PASSWORD

mailozaurr profile summary --profile work-imap --json
mailozaurr profile test --profile work-imap --scope mailbox --json
```

Search and inspect mail:

```powershell
mailozaurr mail folders --profile work-imap --compact --json
mailozaurr mail search --profile work-imap --folder Inbox --query Invoice --compact --json
mailozaurr mail get --profile work-imap --folder Inbox --message-id 123 --compact --json
mailozaurr mail attachments --profile work-imap --folder Inbox --message-id 123 --json
mailozaurr mail save-attachments --profile work-imap --folder Inbox --message-id 123 --path C:\Temp\Attachments --json
```

Draft, send, and queue mail:

```powershell
mailozaurr draft save --draft weekly-update --name "Weekly update" --profile work-smtp `
  --to team@example.com --subject "Weekly update" --text "Status attached." --json

mailozaurr send --draft weekly-update --json
mailozaurr queue list --compact --json
mailozaurr queue process --json
```

Send commands attempt immediate delivery by default. Add `--queue-on-failure` when a failed immediate attempt should be persisted for later processing.

Preview and execute mailbox actions:

```powershell
mailozaurr mail preview-delete --profile work-imap --folder Inbox --message-id 123 --json
mailozaurr mail delete --profile work-imap --folder Inbox --message-id 123 --confirm-token <token> --json

mailozaurr mail preview-move --profile work-imap --folder Inbox --message-id 123 --target-folder Archive --json
mailozaurr mail move --profile work-imap --folder Inbox --message-id 123 --target-folder Archive --confirm-token <token> --json
```

### MCP server usage

Mailozaurr can also run as an MCP server over stdio:

```powershell
mailozaurr mcp serve
```

The MCP surface is built on top of the same shared services as the CLI. Current tool areas include:

- profile and auth management
- folder and folder-alias discovery
- mail search, get, batch get, and attachment save
- draft, send, and queue operations
- mailbox action preview and execution
- action-plan import/export, execution, and stored batch management

A minimal MCP client entry typically points to the executable and passes `mcp serve` as arguments:

```json
{
  "command": "mailozaurr",
  "args": ["mcp", "serve"]
}
```

If your client prefers an absolute path, point it to the built executable instead.

### Skills and MCP

Skills are a good fit on top of the MCP server, but they are not a replacement for the server itself.

- `mailozaurr mcp serve` provides the actual mailbox and send tools
- skills provide workflow guidance, safety rules, and task-specific behavior for an MCP client or agent

In practice, that means Mailozaurr should own the reusable mailbox, queue, send, and action logic, while skills can teach an agent how to use those tools safely and consistently.

This started with a single goal to replace `Send-MailMessage` which is deprecated/obsolete with something more modern, but since MailKit and MimeKit have lots of options why not build on that?

## Support This Project

If you find this project helpful, please consider supporting its development.
Your sponsorship will help the maintainers dedicate more time to maintenance and new feature development for everyone.

It takes a lot of time and effort to create and maintain this project.
By becoming a sponsor, you can help ensure that it stays free and accessible to everyone who needs it.

To become a sponsor, you can choose from the following options:

 - [Become a sponsor via GitHub Sponsors :heart:](https://github.com/sponsors/PrzemyslawKlys)
 - [Become a sponsor via PayPal :heart:](https://paypal.me/PrzemyslawKlys)

Your sponsorship is completely optional and not required for using this project.
We want this project to remain open-source and available for anyone to use for free,
regardless of whether they choose to sponsor it or not.

If you work for a company that uses our .NET libraries or PowerShell Modules,
please consider asking your manager or marketing team if your company would be interested in supporting this project.
Your company's support can help us continue to maintain and improve this project for the benefit of everyone.

Thank you for considering supporting this project!

## Building project for local use
To build a project and play with the sources you need to:

```powershell
Install-Module PSPublishModule -Force -Verbose
```

Once the module is installed - go to Build folder and run: `Manage-Mailozaurr.ps1`.
This will make sure the project has required libraries.

## Features

- Send Email (`Send-EmailMessage`) using:
  - [x] SMTP with standard password
  - [x] SMTP with oAuth2 Office 365
  - [x] SMTP with oAuth2 Google Mail
  - [x] SMTP with SendGrid
  - [x] SendGrid API
  - [x] Amazon SES API
  - [x] Office 365 Graph API
  - [x] PGP/MIME encryption and signature verification
  - Optional retry logic for `Send-EmailMessage` via `-RetryCount`, `-RetryDelayMilliseconds`, `-RetryDelayBackoff` and `-RetryAlways`.  Retries are only attempted for transient errors by default, but `-RetryAlways` forces retries on all errors.
  - Optional SMTP connection pooling via `-UseConnectionPool` and `-ConnectionPoolSize` for faster repeated sends. Use `Clear-SmtpConnectionPool` to reset the pool and `Test-SmtpConnection` to check if your server keeps connections open.
- POP3
  - [x] Connect to POP3
  - [x] Get POP3 Emails
  - [x] Save POP3 Emails
  - [x] Delete POP3 Messages with `Get-POP3Message -Delete`
  - [x] Wait for new POP3 messages with the `Wait-POP3Message` cmdlet supporting `-Until`, `-StopOnMatch` and `-TimeoutSeconds`
  - [x] Search POP3 mailboxes with `Search-POP3Mailbox` returning messages (use `-Count` to limit results)
- IMAP
  - [x] Connect to IMAP
  - [x] Get IMAP Folder
  - [x] List IMAP root folders with `Get-IMAPFolder -Root`
  - [x] Get IMAP Messages
  - [x] Delete IMAP Messages with `Get-IMAPMessage -Delete`
  - [x] Listen for new IMAP messages via `ImapIdleListener`
  - [x] Wait for new IMAP messages with the `Wait-IMAPMessage` cmdlet supporting `-Until`, `-StopOnMatch` and `-TimeoutSeconds`
  - [x] Search IMAP mailboxes with `Search-IMAPMailbox` returning messages (use `-Count` to limit results)
- Office 365 Graph API
  - [x] Get Mail Folders via `Get-EmailGraphFolder` with `-Connection` or `-MgGraphRequest`
  - [x] Get Mail Messages
  - [x] Save Mail Messages
  - [x] Mark messages read or unread with `Set-GraphMessage`
  - [x] Wait for new Graph messages with the `Wait-GraphMessage` cmdlet
  - [x] Search Graph mailboxes with `Search-GraphMailbox` returning message info (use `-Count` to limit results)
  - [x] Message cmdlets output friendly wrappers exposing From, To, Subject, Date and body text
- Pipeline input is supported for Graph connection objects when retrieving
  folders or messages:

```powershell
$graph = Connect-EmailGraph -Credential $cred
$graph | Get-EmailGraphFolder -UserPrincipalName 'user@example.com'
$graph | Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -Limit 10 |
    Save-GraphMessage -Path 'C:\Archive'
```
- DNS and email validation functionality is now provided by [DomainDetective](https://github.com/EvotecIT/DomainDetective)
- [x] Convert between EML and MSG message formats

### Authenticating to IMAP and POP3

Both `Connect-IMAP` and `Connect-POP3` support a variety of sign-in methods. You can use plain credentials, pass a `PSCredential` object, or supply an OAuth token.

```powershell
# Username and password
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'p@ssword'

# Credential object
$cred = Get-Credential
$pop3 = Connect-POP3 -Server 'pop.example.com' -Credential $cred

# OAuth2 token with a SecureString client secret
$googleClientSecret = Read-Host 'Google client secret' -AsSecureString
$oauth = Connect-OAuthGoogle -ClientId 'id' -ClientSecretSecureString $googleClientSecret -GmailAccount 'user@example.com' -Scope https://mail.google.com/
$imapOAuth = Connect-IMAP -Server 'imap.gmail.com' -Credential $oauth -OAuth2
```

### Enumerating IMAP folders

After connecting you can list the top-level folders:

```powershell
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred
Get-IMAPFolder -Client $client -Root
```

Using OAuth tokens works the same:

```powershell
$token = Connect-OAuthGoogle -ClientId 'id' -ClientSecretSecretName 'gmail-client-secret' -ClientSecretVaultName 'MailSecrets' -GmailAccount 'user@example.com' -Scope https://mail.google.com/
$client = Connect-IMAP -Server 'imap.gmail.com' -Credential $token -OAuth2
Get-IMAPFolder -Client $client -Root
```

You can also open a specific folder by path:

```powershell
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred
Get-IMAPFolder -Client $client -Path 'Inbox/Reports'
```

### Listing folder contents

Once a folder is opened you can retrieve its messages using `Get-IMAPMessage`.

```powershell
# Inbox messages
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred
Get-IMAPFolder -Client $client
Get-IMAPMessage -Client $client

# Nested folder
Get-IMAPFolder -Client $client -Path 'Inbox/Reports/2024'
Get-IMAPMessage -Client $client

# Sent and deleted items
Get-IMAPFolder -Client $client -Path 'Sent'
Get-IMAPMessage -Client $client
Get-IMAPFolder -Client $client -Path 'Deleted Items'
Get-IMAPMessage -Client $client
```

### Listening for new IMAP mail

The `ImapIdleListener` class uses the IMAP IDLE command to raise events whenever
new mail arrives. Create an instance with your connected `ImapClient`, subscribe
to `MessageArrived`, and call `StartAsync`:

```csharp
using var client = new ImapClient();
await client.ConnectAsync("imap.example.com", 993, SecureSocketOptions.SslOnConnect);
await client.AuthenticateAsync("user@example.com", "password");

var listener = new Mailozaurr.ImapIdleListener(client);
listener.MessageArrived += (s, msg) =>
    Console.WriteLine($"New message: {msg.Message.Subject}");
await listener.StartAsync();
```

Call `Stop()` to end listening. See
`Sources/Mailozaurr.Examples/ImapIdleListenerExample.cs` for a full example.

Alternatively, use the PowerShell cmdlet `Wait-IMAPMessage` to output new
messages directly to the pipeline:

```powershell
$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred
Wait-IMAPMessage -Client $client -Until { $_.Message.From.Mailboxes.Address -contains 'alice@example.com' } -StopOnMatch -TimeoutSeconds 600 -Action { param($m) "New IMAP from Alice: $($m.Message.Subject)" }
```

Press <kbd>Ctrl+C</kbd> to stop waiting for messages.

The same concept applies to POP3, IMAP and Microsoft Graph mailboxes. `Wait-POP3Message`, `Wait-IMAPMessage` and `Wait-GraphMessage` all support `-Until`, `-StopOnMatch` and `-TimeoutSeconds` parameters so you can wait for specific senders or stop after a period of time:

```powershell
$cred = Get-Credential
$graph = Connect-EmailGraph -Credential $cred
Wait-GraphMessage -Connection $graph -UserPrincipalName 'user@example.com' -Until { $_.from.emailAddress.address -eq 'alice@example.com' } -StopOnMatch -TimeoutSeconds 600 -Action {
    param($m)
    "Graph from Alice: $($m.subject)"
}
```

### Throttling‑safe sending (Graph) and retry/backoff knobs

Mailozaurr v2 includes a built‑in policy for Microsoft Graph sends that handles throttling and transient errors:

- Concurrency limiting to avoid request bursts
- Exponential backoff with jitter and a max cap
- Honors Retry-After header on 429
- Optional SMTP fallback when Graph ultimately fails

PowerShell

```powershell
# Graph send with conservative retry/backoff and concurrency
$cred = ConvertTo-GraphCredential -ClientId $ClientId -SecretName 'graph-client-secret' -VaultName 'MailSecrets' -DirectoryId $TenantId

Send-EmailMessage -Graph -From 'sender@example.com' -To 'user@example.com' `
  -Credential $cred -HTML '<b>Hello</b>' -Subject 'Graph policy demo' `
  -RetryCount 4 -RetryDelayMilliseconds 1000 `
  -JitterMilliseconds 500 -MaxDelayMilliseconds 30000 `
  -GraphMaxConcurrency 2 -Verbose

# Optional: SMTP fallback when Graph keeps failing (configure once)
[Mailozaurr.MailozaurrOptions]::SmtpFallbackFactory = {
  param($graph)
  $s = [Mailozaurr.Smtp]::new()
  $s.Connect('smtp.office365.com', 587)
  $s.Authenticate([System.Net.NetworkCredential]::new('user','pass'))
  $s
}
Send-EmailMessage -Graph -EnableSmtpFallback -From 'sender@example.com' -To 'user@example.com' `
  -Credential $cred -HTML '<b>Hello via fallback</b>' -Subject 'Graph+SMTP fallback'

# Same jitter/max-delay knobs also apply to SMTP/SendGrid/Mailgun/SES
Send-EmailMessage -Server 'smtp.office365.com' -Port 587 -UseSsl `
  -From 'sender@example.com' -To 'user@example.com' -Credential (Get-Credential) `
  -RetryCount 4 -RetryDelayMilliseconds 2000 -JitterMilliseconds 400 -MaxDelayMilliseconds 30000
```

C# (Graph)

```csharp
using Mailozaurr;
var policy = new GraphSendPolicy {
    MaxConcurrency = 2,
    MaxRetries = 4,
    BaseDelayMs = 1000,
    MaxDelayMs = 30000,
    JitterMs = 500,
    RetryOnTransient = true,
    EnableSmtpFallback = true
};

var graph = new Graph()
    .WithSendPolicy(policy)
    .WithSmtpFallback(() => {
        var s = new Smtp();
        s.Connect("smtp.office365.com", 587);
        s.Authenticate("user@example.com", "password");
        return s;
    });

graph.From = "sender@example.com";
graph.To = new object[] { "user@example.com" };
graph.Subject = "Graph policy demo";
graph.HTML = "<b>Hello</b>";
graph.Authenticate(new System.Net.NetworkCredential("clientid@tenant.onmicrosoft.com", "client-secret"));
await graph.ConnectO365GraphAsync();
await graph.SendMessageAsync();
```

Notes

- Defaults are conservative. Override per‑invocation in PowerShell or set `MailozaurrOptions.DefaultGraphPolicy` in code.
- For raw `Invoke‑MgGraphRequest` scenarios, use `-GraphMaxConcurrency` to keep batch operations within Graph tenancy limits.

```powershell
$cred = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $cred
Wait-POP3Message -Client $client -Until { $_.Message.From.Mailboxes.Address -contains 'alice@example.com' } -StopOnMatch -TimeoutSeconds 600 -Action {
    param($m)
    "POP3 from Alice: $($m.Message.Subject)"
}
```

### Using OpenPGP

PGP/MIME encryption allows securing messages using public and private keys. To sign and encrypt a message provide paths to the public and secret key files:

```powershell
$pub = 'Examples/PGPKeys/mimekit.gpg.pub'
$sec = 'Examples/PGPKeys/mimekit.gpg.sec'
Send-EmailMessage -From 'mimekit@example.com' -To 'mimekit@example.com' \ 
    -Server 'smtp.example.com' -Port 25 -Body 'Test' -Subject 'Test' \ 
    -SignOrEncrypt PgpSignAndEncrypt -PublicKeyPath $pub -PrivateKeyPath $sec \ 
    -PrivateKeyPassword 'no.secret'
```

## Documentation

While I didn't spent much time creating WIKI, working on Get-Help documentation, I did write blog articles that should help you get started.

- [x] [Mailozaurr - New mail toolkit (SMTP, IMAP, POP3) with support for oAuth 2.0 and GraphApi for PowerShell](https://evotec.xyz/mailozaurr-new-mail-toolkit-smtp-imap-pop3-with-support-for-oauth-2-0-and-graphapi-for-powershell/)
- [x] [Easy way to send emails using Microsoft Graph API (Office 365) with PowerShell](https://evotec.xyz/easy-way-to-send-emails-using-microsoft-graph-api-office-365-with-powershell/)

You can also utilize Examples which should help to understand use cases. Of course it would be great having pretty help so if you can help me out feel free to submit PR's.
- [Example-SendEmail-SignEncrypt.ps1](Examples/Example-SendEmail-SignEncrypt.ps1) - sign and encrypt an email using SMTP.
- [Example-SendEmail-Pgp.ps1](Examples/Example-SendEmail-Pgp.ps1) - send a PGP encrypted email.
- [Example-SendEmail-OAuthGmail.ps1](Examples/Example-SendEmail-OAuthGmail.ps1) - send mail through Gmail using OAuth2.
- [Example-SendEmail-OAuthO365.ps1](Examples/Example-SendEmail-OAuthO365.ps1) - send mail through Office 365 using OAuth2.
- [Example-GraphManageMessages.ps1](Examples/Example-GraphManageMessages.ps1) - download attachments, set read state and move messages using Microsoft Graph.
- [Example-SetGraphMessage.ps1](Examples/Example-SetGraphMessage.ps1) - mark Graph messages read or unread and move them.
- [Example-SavePop3MessageAttachment.ps1](Examples/Example-SavePop3MessageAttachment.ps1) - download attachments from a POP3 mailbox.
- [Example-SaveImapMessageAttachment.ps1](Examples/Example-SaveImapMessageAttachment.ps1) - download attachments from an IMAP mailbox.
- [Example-SavePop3MessageAttachmentFiltered.ps1](Examples/Example-SavePop3MessageAttachmentFiltered.ps1) - search POP3 messages and save matching attachments.
- [Example-SaveImapMessageAttachmentFiltered.ps1](Examples/Example-SaveImapMessageAttachmentFiltered.ps1) - search IMAP messages and save matching attachments.
- [Example-ListImapRootFolders.ps1](Examples/Example-ListImapRootFolders.ps1) - list root folders using credentials.
- [Example-ListImapRootFoldersOAuth.ps1](Examples/Example-ListImapRootFoldersOAuth.ps1) - list root folders using OAuth.
- [Example-ListImapFolderContents.ps1](Examples/Example-ListImapFolderContents.ps1) - open folders by path and list messages.
- [Example-GetMailFolder.ps1](Examples/Example-GetMailFolder.ps1) - list folders using either `-Connection` or `-MgGraphRequest`.
- [Example-ConnectEmailGraph-DeviceCode.ps1](Examples/Example-ConnectEmailGraph-DeviceCode.ps1) - authenticate with device code.
- [Example-ConnectEmailGraph-OnBehalfOf.ps1](Examples/Example-ConnectEmailGraph-OnBehalfOf.ps1) - exchange a user token for Graph access.
- [Example-SendEmail-GraphDeviceCode.ps1](Examples/Example-SendEmail-GraphDeviceCode.ps1) - interactive device code sign-in and send an email.
- [Example-SendEmail-GraphWithMgRequest.ps1](Examples/Example-SendEmail-GraphWithMgRequest.ps1) - send mail using Connect-MgGraph and the `-MgGraphRequest` switch.
- Use `Connect-EmailGraph` and `Disconnect-EmailGraph` to authenticate and release application credentials for Microsoft Graph. The connection cmdlet supports both client secret and certificate authentication modes.
- `Connect-EmailGraph`, `Connect-IMAP` and `Connect-POP3` store the last successful connection in module variables. Subsequent cmdlets use these defaults when `-Connection` or `-Client` is omitted.
- [Example-SendEmail-Attachments.ps1](Examples/Example-SendEmail-Attachments.ps1) - demonstrate file and in-memory attachments.
- [Example-SendEmail-ConnectionPool.ps1](Examples/Example-SendEmail-ConnectionPool.ps1) - basic connection pool usage.
- [Example-SendEmail-ConnectionPool-Advanced.ps1](Examples/Example-SendEmail-ConnectionPool-Advanced.ps1) - reuse the connection while sending multiple different messages.
- [Example-TestSmtpConnection.ps1](Examples/Example-TestSmtpConnection.ps1) - verify if the server keeps connections alive before enabling pooling.
- [Example-WaitImapMessage.ps1](Examples/Example-WaitImapMessage.ps1) - wait for new mail from PowerShell.
- [Example-DeleteImapMessages.ps1](Examples/Example-DeleteImapMessages.ps1) - delete messages from an IMAP mailbox.
- [Example-DeletePop3Messages.ps1](Examples/Example-DeletePop3Messages.ps1) - delete messages from a POP3 mailbox.
- [Example-ImapFilterScenarios.ps1](Examples/Example-ImapFilterScenarios.ps1) - ten filtering techniques for IMAP.
- [Example-Pop3FilterScenarios.ps1](Examples/Example-Pop3FilterScenarios.ps1) - ten filtering techniques for POP3.
- [ImapIdleListenerExample.cs](Sources/Mailozaurr.Examples/ImapIdleListenerExample.cs) - C# sample listening for new mail.
- [PGP documentation](Docs/PGP.md) - overview of OpenPGP support.
- [OAuth flow documentation](Docs/OAuthFlows.md) - device code and on-behalf-of usage.
- [Configuration and Usage](Docs/Configuration-and-Usage.md) - profile configuration, provider recipes, CLI usage, and MCP usage.

-Some useful examples:
- `Examples/Example-AcquireO365TokenInteractive.ps1` – demonstrates `Connect-OAuthO365`, `ConvertFrom-OAuth2Credential`, and using the token with `Send-EmailMessage`, IMAP, and POP3.
- `Examples/Example-AcquireGoogleTokenInteractive.ps1` – demonstrates `Connect-OAuthGoogle`, `ConvertFrom-OAuth2Credential`, and using the token with `Send-EmailMessage`, IMAP, and POP3.
- `Sources/Mailozaurr.Examples/AcquireO365TokenInteractive.cs` – C# sample for `OAuthHelpers.AcquireO365TokenInteractiveAsync`.
- `Sources/Mailozaurr.Examples/AcquireGoogleTokenInteractive.cs` – C# sample for `OAuthHelpers.AcquireGoogleTokenInteractiveAsync`.
- `Sources/Mailozaurr.Examples/SendEmailAttachments.cs` – C# sample demonstrating attachments and inline resources.

### Choosing between `-Graph` and `-MgGraphRequest`

The module supports two ways of calling Microsoft Graph. Use `-Graph` when you connect with `Connect-EmailGraph` and provide credentials created with `ConvertTo-GraphCredential` or `ConvertTo-GraphCertificateCredential`. The `-MgGraphRequest` switch is meant for scenarios where you authenticate through the Microsoft Graph PowerShell SDK using `Connect-MgGraph`.

| Parameter | Connect command | When to use |
|-----------|-----------------|-------------|
| `-Graph` | `Connect-EmailGraph` | Application or certificate authentication handled by Mailozaurr. |
| `-MgGraphRequest` | `Connect-MgGraph` | Already authenticated via the Microsoft Graph PowerShell SDK. |

**Example using `-Graph`**

```powershell
$cred = ConvertTo-GraphCredential -ClientId $ClientId -SecretName 'graph-client-secret' -VaultName 'MailSecrets' -DirectoryId $TenantId
Connect-EmailGraph -Credential $cred
Send-EmailMessage -From 'user@example.com' -To 'user@example.com' -Subject 'Graph Test' -Graph
Disconnect-EmailGraph
```

**Example using `-MgGraphRequest`**

```powershell
Import-Module Microsoft.Graph.Authentication
Connect-MgGraph -Scopes Mail.Send -NoWelcome
Send-EmailMessage -From 'user@example.com' -To 'user@example.com' -Subject 'Graph Test' -MgGraphRequest
```

### Filtering Graph messages

`Get-EmailGraphMessage` supports the `-Filter` parameter for raw OData queries. This value is passed directly to Microsoft Graph's `$filter` option.

```powershell
Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -Filter "receivedDateTime ge 2024-01-01T00:00:00Z and contains(subject,'Invoice')"
```

See the [Microsoft Graph query parameters documentation](https://learn.microsoft.com/en-us/graph/query-parameters#filter-parameter) for details.

Keep in mind PSSharedGoods is only required for development. When you use this module from PowerShellGallery it's not installed as everything is merged.

### SMTP connection pooling

You can reuse SMTP connections by enabling the connection pool with the `-UseConnectionPool` switch of `Send-EmailMessage`. The `-ConnectionPoolSize` parameter controls how many connections are kept alive. Use `Test-SmtpConnection` to examine the banner and capabilities and to check whether the connection remains open after a `NOOP` command. If the returned object has `Persistent` set to `True`, you can safely enable pooling. Call `Clear-SmtpConnectionPool` whenever you need to discard existing pooled clients.

### Limiting access to mailboxes (Microsoft Graph API)

Microsoft Graph API requires `Send.Mail` permission to send emails. In some cases `Mail.ReadWrite` permission is also required when the size of email is above 4MB.
If the application is registered in Azure AD (Entra ID) without any additional configuration, it will have access to all mailboxes in the organization.
To limit access to specific mailboxes, you need to use Application Access Policy.
This is a feature that allows you to limit access to specific mailboxes. You can read more about it [here](https://learn.microsoft.com/en-us/graph/auth-limit-mailbox-access).

Following permissions (as shown on the screenshot) are required to send emails using Microsoft Graph API.

![Microsoft Graph Permissions](https://raw.githubusercontent.com/EvotecIT/Mailozaurr/refs/heads/v2-speedygonzales/Docs/Images/MicrosoftGraphPermissions.png)

The rest of the permissions is not required and is there for other features that Microsoft Graph provides.

## To install

```powershell
Install-Module -Name Mailozaurr -AllowClobber -Force
```

Force and AllowClobber aren't necessary, but they do skip errors in case some appear.

## And to update

```powershell
Update-Module -Name Mailozaurr
```

That's it. Whenever there's a new version, you run the command, and you can enjoy it. Remember that you may need to close, reopen PowerShell session if you have already used module before updating it.

**The essential thing** is if something works for you on production, keep using it till you test the new version on a test computer. I do changes that may not be big, but big enough that auto-update may break your code. For example, a small rename to a parameter, and your code stops working! Be responsible!
