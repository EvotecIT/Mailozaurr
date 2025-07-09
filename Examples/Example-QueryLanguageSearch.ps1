# Demonstrates searching an IMAP mailbox using query language
Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$imap = Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential)

# search using query string for messages from boss with subject containing "report"
$result = Search-IMAPMailbox -Client $imap -Query 'from:boss subject:"report"'

$result | ForEach-Object {
    Write-Host "Found message $($_.Message.Subject)"
}

Disconnect-IMAP -Client $imap
