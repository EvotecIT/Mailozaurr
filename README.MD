<p align="center">
  <a href="https://dev.azure.com/evotecpl/Mailozaurr/_build/results?buildId=latest"><img src="https://dev.azure.com/evotecpl/Mailozaurr/_apis/build/status/EvotecIT.Mailozaurr"></a>
  <a href="https://www.powershellgallery.com/packages/Mailozaurr"><img src="https://img.shields.io/powershellgallery/v/Mailozaurr.svg"></a>
  <a href="https://www.powershellgallery.com/packages/Mailozaurr"><img src="https://img.shields.io/powershellgallery/vpre/Mailozaurr.svg?label=powershell%20gallery%20preview&colorB=yellow"></a>
  <a href="https://github.com/EvotecIT/Mailozaurr"><img src="https://img.shields.io/github/license/EvotecIT/Mailozaurr.svg"></a>
</p>

<p align="center">
  <a href="https://www.powershellgallery.com/packages/Mailozaurr"><img src="https://img.shields.io/powershellgallery/p/Mailozaurr.svg"></a>
  <a href="https://github.com/EvotecIT/Mailozaurr"><img src="https://img.shields.io/github/languages/top/evotecit/Mailozaurr.svg"></a>
  <a href="https://github.com/EvotecIT/Mailozaurr"><img src="https://img.shields.io/github/languages/code-size/evotecit/Mailozaurr.svg"></a>
  <a href="https://www.powershellgallery.com/packages/Mailozaurr"><img src="https://img.shields.io/powershellgallery/dt/Mailozaurr.svg"></a>
</p>

<p align="center">
  <a href="https://twitter.com/PrzemyslawKlys"><img src="https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social"></a>
  <a href="https://evotec.xyz/hub"><img src="https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg"></a>
  <a href="https://www.linkedin.com/in/pklys"><img src="https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn"></a>
</p>

# Mailozaurr - PowerShell Module

Mailozaurr is a PowerShell module that aims to provide SMTP, POP3, IMAP and few other ways to interact with Email. Underneath it uses MimeKit and MailKit and EmailValidation libraries written by Jeffrey Stedfast.

This project has 2 branches:
- `v1-legacy` - this is the old version of the module. It's working, it's stable and has been around for a while. It's written entirely in PowerShell, and has nice features. It will be maintained until v2 is ready.
- `v2-speedygonzales` - this is the new version of the module. It's a complete rewrite. It's written in C#, and slowly all functions from V1 will be replaced by their C# alternative. It's faster, and has couple of new features. It's still in development, but it's already usable.
  - Removed **DNS** functions as they are moved to [DnsClientX](https://github.com/EvotecIT/DnsClientX) module
  - Removed **EmailValidation/SPF/DKIM/DMARC/MX/DMARC** functions as they are moved to [DomainDetective](https://github.com/EvotecIT/DomainDetective) module

This **V2** branch uses following libraries:

- [MailKit](https://github.com/jstedfast/MailKit) - licensed MIT
- [MimeKit](https://github.com/jstedfast/MimeKit) - licensed MIT
- [MsgKit](https://github.com/Sicos1977/MsgKit) - licensed MIT
- [MsgReader](https://github.com/Sicos1977/MSGReader) - licensed MIT

For oAuth2 it also requires some Microsoft and Google libraries that are also bundled in

- [Microsoft.Identity.Client](https://www.nuget.org/packages/Microsoft.Identity.Client/)
- [Google.Apis](https://www.nuget.org/packages/Google.Apis)
- [Google.Apis.Core](https://www.nuget.org/packages/Google.Apis.Core)
- [Google.Apis.Auth](https://www.nuget.org/packages/Google.Apis.Auth)

Additional dependencies due to features added

- [System.DataSQLite](https://system.data.sqlite.org/)

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
  - [x] Office 365 Graph API
  - [x] PGP/MIME encryption and signature verification
  - Optional retry logic for `Send-EmailMessage` via `-RetryCount`, `-RetryDelayMilliseconds`, `-RetryDelayBackoff` and `-RetryAlways`.  Retries are only attempted for transient errors by default, but `-RetryAlways` forces retries on all errors.
- POP3
  - [x] Connect to POP3
  - [x] Get POP3 Emails
  - [x] Save POP3 Emails
- IMAP
  - [x] Connect to IMAP
  - [x] Get IMAP Folder
  - [x] Get IMAP Messages
  - [x] Listen for new IMAP messages via `ImapIdleListener`
  - [x] Wait for new IMAP messages with the `Wait-IMAPMessage` cmdlet
- Office 365 Graph API
  - [x] Get Mail Folders via `Get-EmailGraphFolder` with `-Connection` or `-MgGraphRequest`
  - [x] Get Mail Messages
  - [x] Save Mail Messages
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
Wait-IMAPMessage -Client $client -Action { param($m) "New: $($m.Message.Subject)" }
```

Press <kbd>Ctrl+C</kbd> to stop waiting for messages.

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
- [Example-GetMailFolder.ps1](Examples/Example-GetMailFolder.ps1) - list folders using either `-Connection` or `-MgGraphRequest`.
- [Example-ConnectEmailGraph-DeviceCode.ps1](Examples/Example-ConnectEmailGraph-DeviceCode.ps1) - authenticate with device code.
- [Example-ConnectEmailGraph-OnBehalfOf.ps1](Examples/Example-ConnectEmailGraph-OnBehalfOf.ps1) - exchange a user token for Graph access.
- [Example-SendEmail-GraphWithMgRequest.ps1](Examples/Example-SendEmail-GraphWithMgRequest.ps1) - send mail using Connect-MgGraph and the `-MgGraphRequest` switch.
- Use `Connect-EmailGraph` and `Disconnect-EmailGraph` to authenticate and release application credentials for Microsoft Graph. The connection cmdlet supports both client secret and certificate authentication modes.
- `Connect-EmailGraph`, `Connect-IMAP` and `Connect-POP3` store the last successful connection in module variables. Subsequent cmdlets use these defaults when `-Connection` or `-Client` is omitted.
- [Example-SendEmail-Attachments.ps1](Examples/Example-SendEmail-Attachments.ps1) - demonstrate file and in-memory attachments.
- [Example-WaitImapMessage.ps1](Examples/Example-WaitImapMessage.ps1) - wait for new mail from PowerShell.
- [ImapIdleListenerExample.cs](Sources/Mailozaurr.Examples/ImapIdleListenerExample.cs) - C# sample listening for new mail.
- [PGP documentation](Docs/PGP.md) - overview of OpenPGP support.
- [OAuth flow documentation](Docs/OAuthFlows.md) - device code and on-behalf-of usage.

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
$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
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
