Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$credential = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $credential -Port 995 -Options Auto

# Delete messages older than 30 days that contain "Report" in the subject
Get-POP3Message -Client $client -Subject 'Report' -Before (Get-Date).AddDays(-30) -Delete

Disconnect-POP3 -Client $client
