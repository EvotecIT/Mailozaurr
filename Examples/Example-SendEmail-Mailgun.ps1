Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Use Mailgun API
$Key = Get-Content -Raw -Path "C:\Support\Important\Mailgun.txt"
$Credential = ConvertTo-MailgunCredential -ApiKey $Key

$sendEmailMessageSplat = @{
    From          = 'postmaster@sandbox814085ede3524b939d4b7f518ef9877a.mailgun.org'
    To            = 'przemyslaw.klys+mailgun@xxx.pl'
    Subject       = 'Mailgun Test'
    HTML          = 'Hello from Mailgun'
    Credential    = $Credential
    Verbose       = $true
    EmailProvider = 'Mailgun'
    WhatIf        = $false
}

Send-EmailMessage @sendEmailMessageSplat
