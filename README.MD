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

This started with a single goal to replace `Send-MailMessage` which is depracated/obsolete with something more modern, but since MailKit and MimeKit have lots of options why not build on that?

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
  - [x] DNSBL Reporting (`Find-DNSBL`)
- Email Validation
  - [x] Test Email Address for errors

## Documentation

While I didn't spent much time creating WIKI, working on Get-Help documentation, I did write blog articles that should help you get started.

- [x] [Mailozaurr – New mail toolkit (SMTP, IMAP, POP3) with support for oAuth 2.0 and GraphApi for PowerShell](https://evotec.xyz/mailozaurr-new-mail-toolkit-smtp-imap-pop3-with-support-for-oauth-2-0-and-graphapi-for-powershell/)
- [x] [Easy way to send emails using Microsoft Graph API (Office 365) with PowerShell](https://evotec.xyz/easy-way-to-send-emails-using-microsoft-graph-api-office-365-with-powershell/)

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
