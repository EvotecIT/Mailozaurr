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

- [MailKit](https://github.com/jstedfast/MailKit)
- [MimeKit](https://github.com/jstedfast/MimeKit)
- [EmailValidation](https://github.com/jstedfast/EmailValidation)

For oAuth2 it also requires some Microsoft and Google libraries that are also bundled in

- [Microsoft.Identity.Client](https://www.nuget.org/packages/Microsoft.Identity.Client/)
- [Google.Apis](https://www.nuget.org/packages/Google.Apis)
- [Google.Apis.Core](https://www.nuget.org/packages/Google.Apis.Core)
- [Google.Apis.Auth](https://www.nuget.org/packages/Google.Apis.Auth)

Finally since `Resolve-DNSName` is Windows specific I'm using:

- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET)

This started with a single goal to replace `Send-MailMessage` which is depracated/obsolete with something more modern, but since MailKit and MimeKit have lots of options why not build on that?

## Features

- Send Email (`Send-EmailMessage`) using:
  - [x] SMTP with standard password
  - [x] SMTP with oAuth2 Office 365
  - [x] SMTP with oAuth2 Google Mail
  - [x] SMTP with SendGrid
  - [x] SendGrid API
  - [x] Office 365 Graph API
- POP3
  - [x] Connect to POP3
  - [x] Get POP3 Emails
  - [x] Save POP3 Emails
- IMAP
  - [x] Connect to IMAP
  - [x] Get IMAP Folder
  - [ ] Get Messages (you have to your own work to get what you need). Needs improvements!
- Office 365 Graph API
  - [x] Get Mail Folders
  - [x] Get Mail Messages
  - [x] Save Mail Messages
- DNS Checks via TCP/UDP, but also via HTTPS using Cloudflare/Google providers
  - [x] DKIM Reporting (`Find-DKIMRecord`)
  - [x] MX Reporting (`Find-MXRecord`)
  - [x] DMARC Reporting (`Find-DMARCRecord`)
  - [x] SPF Reporting (`Find-SPFRecord`)
- Email Validation
  - [x] Test Email Address for errors

## Documentation

While I didn't spent much time creating WIKI, working on Get-Help documentation, I did write blog articles that should help you get started.

- [x] [Mailozaurr – New mail toolkit (SMTP, IMAP, POP3) with support for oAuth 2.0 and GraphApi for PowerShell](https://evotec.xyz/mailozaurr-new-mail-toolkit-smtp-imap-pop3-with-support-for-oauth-2-0-and-graphapi-for-powershell/)

You can also utilize Examples which should help to understand use cases. Of course it would be great having pretty help so if you can help me out feel free to submit PR's.

Keep in mind PSSharedGoods is only required for development. When you use this module from PowerShellGallery it's not installed as everything is merged.

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

## Changelog

- 0.0.10 - Unreleased
  - `Send-EmailMessage` - fix for Graph where attachments where not attached and nobody reported
  - `Send-EmailMessage` - updated error messages with tips what could be wrong
  - `Send-EmailMessage` - updated ErrorAction Stop in few places for those that prefer errors
  - `Send-EmailMessage` - added basic support for SendGrid with parameter `-SendGrid`
  - `Find-MXRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Find-SPFRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Find-DKIMRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Find-DMARCRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Resolve-DNSRequestRest` - Added just in case
- 0.0.9 - 2020.08.11
  - DNS Records
    - Fixed `DNSServer` parameter usage [#5](https://github.com/EvotecIT/Mailozaurr/issues/5)
- 0.0.8 - 2020.08.06
  - MS Graph API
    - Added `Get-MailFolder` - work in progress
    - Added `Get-MailMessage` - work in progress
    - Added `Save-MailMessage` - work in progress
    - Added check for attachments/message body less than 10 characters. - by andrew0wells [#3](https://github.com/EvotecIT/Mailozaurr/issues/3)
  - SMTP
    - Updated `Send-EmailMessage` error handling
- 0.0.7 - 2020.08.04
  - More updates making it better toolkit
- 0.0.6 - 2020.08.03
  - Public release
  - Added GraphAPI support for Send-EmailMessage
  - Added oAuth2 support for Send-EmailMessage
  - Added oAuth2 support for POP3
  - Added oAuth2 support for IMAP4
  - Fixed lots of stuff
- 0.0.3 - 2020.07.26
  - Added `Test-EmailAddress`
  - Added `Connect-OAuthGoogle`
  - Added `Connect-OAuthO365`
  - Added `Resolve-DNSQuery`
- 0.0.2 - 2020.07.25
  - Added `Find-MXRecord`
  - Added `Find-DMARCRecord`
  - Added `Find-DKIMRecord`
  - Added `Find-SPFRecord`
- 0.0.1 - 2020.06.13
  - Initial release
