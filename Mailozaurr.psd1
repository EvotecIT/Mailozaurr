@{
    AliasesToExport      = @('Connect-POP3', 'Disconnect-POP3', 'Find-BlackList', 'Find-BlockList', 'Find-O365TenantID', 'Get-POP3Message', 'Resolve-DnsRestQuery', 'Save-POP3Message')
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @()
    CompanyName          = 'Evotec'
    CompatiblePSEditions = @('Desktop', 'Core')
    Copyright            = '(c) 2011 - 2024 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description          = 'Mailozaurr is a PowerShell module that aims to provide SMTP, POP3, IMAP and few other ways to interact with Email. Underneath it uses MimeKit and MailKit and EmailValidation libraries written by Jeffrey Stedfast.            '
    FunctionsToExport    = @('Connect-IMAP', 'Connect-oAuthGoogle', 'Connect-oAuthO365', 'Connect-POP', 'ConvertTo-GraphCredential', 'ConvertTo-OAuth2Credential', 'ConvertTo-SendGridCredential', 'Disconnect-IMAP', 'Disconnect-POP', 'Find-AutoDiscoverRecord', 'Find-BIMIRecord', 'Find-DANERecord', 'Find-DKIMRecord', 'Find-DMARCRecord', 'Find-DNSBL', 'Find-LyncRecord', 'Find-MTASTSRecord', 'Find-MxRecord', 'Find-O365OpenIDRecord', 'Find-SecurityTxtRecord', 'Find-SPFRecord', 'Find-TeamsFederationRecord', 'Find-TLSRPTRecord', 'Get-IMAPFolder', 'Get-IMAPMessage', 'Get-MailFolder', 'Get-MailMessage', 'Get-POPMessage', 'Get-StartTLS', 'Resolve-DnsQuery', 'Resolve-DnsQueryRest', 'Save-MailMessage', 'Save-POPMessage', 'Send-EmailMessage', 'Test-EmailAddress', 'Test-EmailAddressConnection')
    GUID                 = '2b0ea9f1-3ff1-4300-b939-106d5da608fa'
    ModuleVersion        = '1.1.0'
    PowerShellVersion    = '5.1'
    PrivateData          = @{
        PSData = @{
            ExternalModuleDependencies = @('Microsoft.PowerShell.Utility', 'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Security')
            IconUri                    = 'https://evotec.xyz/wp-content/uploads/2020/07/MailoZaurr.png'
            Prerelease                 = 'Preview3'
            ProjectUri                 = 'https://github.com/EvotecIT/MailoZaurr'
            Tags                       = @('Windows', 'MacOS', 'Linux', 'Mail', 'Email', 'MX', 'SPF', 'DMARC', 'DKIM', 'GraphApi', 'SendGrid', 'Graph', 'IMAP', 'POP3')
        }
    }
    RequiredModules      = @(@{
            Guid          = 'ee272aa8-baaa-4edf-9f45-b6d6f7d844fe'
            ModuleName    = 'PSSharedGoods'
            ModuleVersion = '0.0.274'
        }, 'Microsoft.PowerShell.Utility', 'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Security')
    RootModule           = 'Mailozaurr.psm1'
}