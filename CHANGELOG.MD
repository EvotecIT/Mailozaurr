#### 1.1.0 - Preview2
- Update to DLLs to latest versions
- Removes some unnessecary DLLs

#### 1.1.0 - Preview1
- Different way to build module and load depenedencies
- Removal of DLLs from source codes
- Small updates to DLLs

#### 1.0.2 - 2023.08.05
- Rename `SkipCertificateValidatation` to `SkipCertificateValidation` in `Send-EmailMessage` (typo fix)
- Added `SkipCertificateValidation` to `Connect-IMAP`
- Added `SkipCertificateValidation` to `Connect-POP3`
- Added `SkipCertificateRevocation` to `Connect-IMAP`
- Added `SkipCertificateRevocation` to `Connect-POP3`
- Use simpler certificate validation callback by @jborean93 in https://github.com/EvotecIT/Mailozaurr/pull/42

#### 1.0.1 - 2023.07.19
- Updated `Microsoft.Identity.Client.dll` to 4.44 to match `ExchangeOnlineManagement` module

#### 1.0.0 - 2022.12.20
- Add support for sending emails with graph using Delegated permissions. It uses Microsoft.Graph.Authentication module and `MgGraphRequest` switch on `Send-EmailMessage`.

```powershell
Import-Module Mailozaurr
Import-Module Microsoft.Graph.Authentication -Force

# this shows how to send email using combination of Mailozaurr and Microsoft.Graph to use Connect-MgGraph to authorize
$Body = EmailBody {
    New-HTMLText -Text "This is test of Connect-MGGraph functionality"
}

# authorize via Connect-MgGraph with delegated rights or any other supported method
Connect-MgGraph -Scopes Mail.Send

# sending email
$sendEmailMessageSplat = @{
    From           = 'przemyslaw.klys@test.pl'
    To             = 'przemyslaw.klys@test.pl'
    HTML           = $Body
    Subject        = 'This tests email as delegated'
    MgGraphRequest = $true
    Verbose        = $true
}
Send-EmailMessage @sendEmailMessageSplat
```

#### 0.9.0 - 2022.10.02
- Fix `Suppress` for sending emails via SendGrid
- Fix `Suppress` for sending emails via Microsoft Graph
- Add support to `Send-EmailMessage` for attachments **4MB** to **150MB** in size for Microsoft Graph. Before it would only send attachments up to **4MB** in size. Detects size automatically and uses the appropriate API endpoint.
- Add `RequestReadReceipt` support to `Send-EmailMessage` for Microsoft Graph to request a read receipt from the recipient.
- Add `RequestDeliveryReceipt` switch to `Send-EmailMessage` for Microsoft Graph to request a delivery receipt. SMTP uses `DeliveryNotificationOption` and `DeliveryStatusNotificationType`.
- Require `Send-EmailMessage` to have From field [#33](https://github.com/EvotecIT/Mailozaurr/issues/33)
- Warn if attachment in `Send-EmailMessage` doesn't exists, no error is thrown [#34](https://github.com/EvotecIT/Mailozaurr/issues/34)
- Fixes special chars issue in file names [#26](https://github.com/EvotecIT/Mailozaurr/issues/26)
-

#### 0.0.25 - 2022.06.07
- Updated `MailKit`
- Updated `MimeKit`
- Added missing libraries so that the project can work on more systems without installing higher version of .NET Framework
- Added tests for SMTP with Auth and Graph API

#### 0.0.24 - 2021.01.24
  - Improved logging logic for `Send-EmailMessage`
#### 0.0.23 - 2021.01.22
  - Added option to `Send-EmailMessage` to specify the `LocalDomain` to be able to troubleshoot issue [#1314](https://github.com/jstedfast/MailKit/issues/1314)
  - Added option to `Send-EmailMessage` - `LogConsole` and `LogObject` which joins `LogPath` in saving whole conversation to console, or to final object as Message property

#### 0.0.22 - 2021.01.21
  - Upgraded `MailKit\MimeKit` to 3.1.0
  - Added support to save MimeMessage in `Send-EmailMessage` using `MimeMessagePath`
  - Fixed `Cloudflare DNS calls` for `Find-DMARCRecord`,`Find-DKIMRecord`, `Find-MXRecord`, `Find-SPFRecord`, `Find-DNSBNL`
  - Updated docs
  - More output fields on `Send-EmailMessage`

#### 0.0.21 - 2022.01.07
  - Added support for logging in the `Send-EmailMessage` for SMTP to allow for debugging.
    - By adding `LogPath $PSScriptRoot\Output\Log1.txt` log file will be created with full information
    - Parameters such as `LogSecrets`, `LogTimeStamps`, `LogTimeStampsFormat` and `LogClientPrefix`,`LogServerPrefix` are available
#### 0.0.20 - 2022.01.02
  - Added support for using `Get-MsalToken` instead of built-in token creation for GraphApi

```powershell
# creating body for HTML using PSWriteHTML
$Body = EmailBody {
    New-HTMLText -Text "this is my test"
    New-HTMLText -Text "Łączy nas piłka"
}

# Generating token using Get-MsalToken
$getMsalTokenSplat = @{
    ClientId     = '0fb383f1'
    ClientSecret = 'VKDM_' | ConvertTo-SecureString -AsPlainText
    Authority    = 'https://login.microsoftonline.com/ceb371f6'
}
$MsalToken = Get-MsalToken @getMsalTokenSplat

# Converting it to Credentials object (so that Send-EmailMessage can use it)
$Credential = ConvertTo-GraphCredential -MsalToken $MsalToken.AccessToken

# sending email
Send-EmailMessage -From 'przemyslaw.klys@something.pl' -To 'przemyslaw.klys@something.else' -Credential $Credential -HTML $Body -Subject 'This is another test email 2' -Graph -Verbose -Priority Low -DoNotSaveToSentItems
```

#### 0.0.19 - 2021.12.25
  - Fixes encoding for Send-EmailMessage wen using GraphApi (forces UTF8)
#### 0.0.18 - 2021.09.23
  - Improved error handling of `Send-EmailMessage` when sending with GraphApi
#### 0.0.17 - 2021.09.19
  - Upgraded `MailKit/Mimekit` `2.15.0`
  - Upgraded `DNSClient` to `1.5.0`
  - Fixes `oAuth2` [#17](https://github.com/EvotecIT/Mailozaurr/issues/17)
  - Removed wrong parameterset from `Send-EmailMessage`
  - Improved `ConvertTo-GraphCredential` to support encrypted graph client secret.
#### 0.0.16 - 2021.07.23
  - Moved Class.MySmtpClient to be loaded from separate file via dot sourcing - hopefully fixes [#16](https://github.com/EvotecIT/Mailozaurr/issues/16)
#### 0.0.15 - 2021.07.18
  - Moved Class.MySmtpClient to be loaded as last in PSM1. This should make it load when loaded by other modules - hopefully!
#### 0.0.14 - 2021.06.28
  - Added missing library `System.Buffers`
#### 0.0.13 - 2021.06.27
  - Added `SkipCertificateValidatation` to `Send-EmailMessage`
  - Downgraded `MailKit/MimeKit 2.12.0`
#### 0.0.12 - 2021.06.20
  - Added `AsSecureString` to `Send-EmailMessage` which allows to provide `-AsSecureString -Username 'przemyslaw.klys@domain.pl' -Password $secStringPassword`
#### 0.0.11 - 2021.06.18
  - Added `SkipCertificateRevocation` to `Send-EmailMessage` [#13](https://github.com/EvotecIT/Mailozaurr/issues/13)
  - 🐛 Fixed PTR records in `Find-MXRecord` when using HTTPS
  - Small improvement to `Send-EmailMessage` returning object when using `WhatIf`
#### 0.0.10 - 2020.10.25
  - `Send-EmailMessage` - fix for Graph where attachments where not attached and nobody reported
  - `Send-EmailMessage` - updated error messages with tips what could be wrong
  - `Send-EmailMessage` - updated ErrorAction Stop in few places for those that prefer errors
  - `Send-EmailMessage` - added basic support for SendGrid with parameter `-SendGrid`
  - `Find-MXRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Find-SPFRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Find-DKIMRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Find-DMARCRecord` - updated with `DNSProvider` (`Cloudflare`/`Google`) for calls over HTTPS
  - `Find-DNSBL` - added, same options as above
  - `Resolve-DNSRequestRest` - Added just in case
#### 0.0.9 - 2020.08.11
  - DNS Records
    - Fixed `DNSServer` parameter usage [#5](https://github.com/EvotecIT/Mailozaurr/issues/5)
#### 0.0.8 - 2020.08.06
  - MS Graph API
    - Added `Get-MailFolder` - work in progress
    - Added `Get-MailMessage` - work in progress
    - Added `Save-MailMessage` - work in progress
    - Added check for attachments/message body less than 10 characters. - by andrew0wells [#3](https://github.com/EvotecIT/Mailozaurr/issues/3)
  - SMTP
    - Updated `Send-EmailMessage` error handling
#### 0.0.7 - 2020.08.04
  - More updates making it better toolkit
#### 0.0.6 - 2020.08.03
  - Public release
  - Added GraphAPI support for Send-EmailMessage
  - Added oAuth2 support for Send-EmailMessage
  - Added oAuth2 support for POP3
  - Added oAuth2 support for IMAP4
  - Fixed lots of stuff
#### 0.0.3 - 2020.07.26
  - Added `Test-EmailAddress`
  - Added `Connect-OAuthGoogle`
  - Added `Connect-OAuthO365`
  - Added `Resolve-DNSQuery`
#### 0.0.2 - 2020.07.25
  - Added `Find-MXRecord`
  - Added `Find-DMARCRecord`
  - Added `Find-DKIMRecord`
  - Added `Find-SPFRecord`
#### 0.0.1 - 2020.06.13
  - Initial release
