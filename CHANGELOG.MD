### Mailozaurr ChangeLog

#### - 0.0.20
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

#### - 0.0.19 - 2021.12.25
  - Fixes encoding for Send-EmailMessage wen using GraphApi (forces UTF8)
#### - 0.0.18 - 2021.09.23
  - Improved error handling of `Send-EmailMessage` when sending with GraphApi
#### - 0.0.17 - 2021.09.19
  - Upgraded `MailKit/Mimekit` `2.15.0`
  - Upgraded `DNSClient` to `1.5.0`
  - Fixes `oAuth2` [#17](https://github.com/EvotecIT/Mailozaurr/issues/17)
  - Removed wrong parameterset from `Send-EmailMessage`
  - Improved `ConvertTo-GraphCredential` to support encrypted graph client secret.
#### - 0.0.16 - 2021.07.23
  - Moved Class.MySmtpClient to be loaded from separate file via dot sourcing - hopefully fixes [#16](https://github.com/EvotecIT/Mailozaurr/issues/16)
#### - 0.0.15 - 2021.07.18
  - Moved Class.MySmtpClient to be loaded as last in PSM1. This should make it load when loaded by other modules - hopefully!
#### - 0.0.14 - 2021.06.28
  - Added missing library `System.Buffers`
#### - 0.0.13 - 2021.06.27
  - Added `SkipCertificateValidatation` to `Send-EmailMessage`
  - Downgraded `MailKit/MimeKit 2.12.0`
#### - 0.0.12 - 2021.06.20
  - Added `AsSecureString` to `Send-EmailMessage` which allows to provide `-AsSecureString -Username 'przemyslaw.klys@domain.pl' -Password $secStringPassword`
#### - 0.0.11 - 2021.06.18
  - Added `SkipCertificateRevocation` to `Send-EmailMessage` [#13](https://github.com/EvotecIT/Mailozaurr/issues/13)
  - 🐛 Fixed PTR records in `Find-MXRecord` when using HTTPS
  - Small improvement to `Send-EmailMessage` returning object when using `WhatIf`
#### - 0.0.10 - 2020.10.25
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
#### - 0.0.9 - 2020.08.11
  - DNS Records
    - Fixed `DNSServer` parameter usage [#5](https://github.com/EvotecIT/Mailozaurr/issues/5)
#### - 0.0.8 - 2020.08.06
  - MS Graph API
    - Added `Get-MailFolder` - work in progress
    - Added `Get-MailMessage` - work in progress
    - Added `Save-MailMessage` - work in progress
    - Added check for attachments/message body less than 10 characters. - by andrew0wells [#3](https://github.com/EvotecIT/Mailozaurr/issues/3)
  - SMTP
    - Updated `Send-EmailMessage` error handling
#### - 0.0.7 - 2020.08.04
  - More updates making it better toolkit
#### - 0.0.6 - 2020.08.03
  - Public release
  - Added GraphAPI support for Send-EmailMessage
  - Added oAuth2 support for Send-EmailMessage
  - Added oAuth2 support for POP3
  - Added oAuth2 support for IMAP4
  - Fixed lots of stuff
#### - 0.0.3 - 2020.07.26
  - Added `Test-EmailAddress`
  - Added `Connect-OAuthGoogle`
  - Added `Connect-OAuthO365`
  - Added `Resolve-DNSQuery`
#### - 0.0.2 - 2020.07.25
  - Added `Find-MXRecord`
  - Added `Find-DMARCRecord`
  - Added `Find-DKIMRecord`
  - Added `Find-SPFRecord`
#### - 0.0.1 - 2020.06.13
  - Initial release
