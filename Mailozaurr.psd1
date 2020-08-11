@{
    AliasesToExport      = 'Connect-POP3', 'Disconnect-POP3', 'Get-POP3Message', 'Save-POP3Message'
    Author               = 'Przemyslaw Klys'
    CompanyName          = 'Evotec'
    CompatiblePSEditions = 'Desktop', 'Core'
    Copyright            = '(c) 2011 - 2020 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description          = 'Mailozaurr is a PowerShell module that aims to provide SMTP, POP3, IMAP and few other ways to interact with Email. Underneath it uses MimeKit and MailKit and EmailValidation libraries written by Jeffrey Stedfast.            '
    FunctionsToExport    = 'Connect-IMAP', 'Connect-oAuthGoogle', 'Connect-oAuthO365', 'Connect-POP', 'ConvertTo-GraphCredential', 'ConvertTo-OAuth2Credential', 'Disconnect-IMAP', 'Disconnect-POP', 'Find-DKIMRecord', 'Find-DMARCRecord', 'Find-MxRecord', 'Find-SPFRecord', 'Get-IMAPFolder', 'Get-IMAPMessage', 'Get-MailFolder', 'Get-MailMessage', 'Get-POPMessage', 'Resolve-DnsQuery', 'Save-MailMessage', 'Save-POPMessage', 'Send-EmailMessage', 'Test-EmailAddress'
    GUID                 = '2b0ea9f1-3ff1-4300-b939-106d5da608fa'
    ModuleVersion        = '0.0.9'
    PowerShellVersion    = '5.1'
    PrivateData          = @{
        PSData = @{
            Tags                       = 'Windows', 'MacOS', 'Linux', 'Mail', 'Email', 'MX', 'SPF', 'DMARC', 'DKIM'
            ProjectUri                 = 'https://github.com/EvotecIT/MailoZaurr'
            ExternalModuleDependencies = 'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Security', 'Microsoft.PowerShell.Utility'
            IconUri                    = 'https://evotec.xyz/wp-content/uploads/2020/07/MailoZaurr.png'
        }
    }
    RequiredModules      = 'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Security', 'Microsoft.PowerShell.Utility'
    RootModule           = 'Mailozaurr.psm1'
}