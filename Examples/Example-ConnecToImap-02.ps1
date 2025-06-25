Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Credential = Get-Credential
$Client = Connect-IMAP -Server 'imap.gmail.com' -Credential $Credential -Port 993 -Options Auto
Get-IMAPFolder -Client $Client -Verbose
Get-IMAPMessage -Client $Client -Subject 'Alert' -Since (Get-Date).AddDays(-1)

Disconnect-IMAP -Client $Client