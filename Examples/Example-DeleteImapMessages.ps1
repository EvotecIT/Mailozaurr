Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Credential = Get-Credential
$Client = Connect-IMAP -Server 'imap.example.com' -Credential $Credential -Port 993 -Options Auto

# Delete messages from the last week containing "Report" in the subject
Get-IMAPMessage -Client $Client -Subject 'Report' -Since (Get-Date).AddDays(-7) -Delete

Disconnect-IMAP -Client $Client

