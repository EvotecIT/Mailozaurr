@{
    AliasesToExport      = 'Connect-POP3', 'Disconnect-POP3', 'Get-POP3Message', 'Save-POP3Message'
    Author               = 'Przemyslaw Klys'
    CompanyName          = 'Evotec'
    CompatiblePSEditions = 'Desktop', 'Core'
    Copyright            = '(c) 2011 - 2020 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description          = 'Mailing helper'
    FunctionsToExport    = 'Connect-IMAP', 'Connect-oAuthGoogle', 'Connect-oAuthO365', 'Connect-POP', 'Disconnect-IMAP', 'Disconnect-POP', 'Find-DKIMRecord', 'Find-DMARCRecord', 'Find-MxRecord', 'Find-SPFRecord', 'Get-IMAPFolder', 'Get-IMAPMessage', 'Get-POPMessage', 'Save-POPMessage', 'Send-EmailMessage', 'Test-EmailAddress'
    GUID                 = '2b0ea9f1-3ff1-4300-b939-106d5da608fa'
    ModuleVersion        = '0.0.3'
    PowerShellVersion    = '5.1'
    PrivateData          = @{
        PSData = @{
            Tags                       = 'Windows', 'MacOS', 'Linux', 'Mail', 'Email'
            ExternalModuleDependencies = 'Microsoft.PowerShell.Utility', 'DnsClient'
        }
    }
    RequiredModules      = 'Microsoft.PowerShell.Utility', 'DnsClient'
    RootModule           = 'Mailozaurr.psm1'
}