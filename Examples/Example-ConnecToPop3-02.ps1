Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Credentials = Get-Credential
$Client = Connect-POP3 -Server 'pop.gmail.com' -Credential $Credentials -Port 995 -Options Auto
Get-POP3Message -Client $Client -Index 0 -Count 5 | Format-Table
Save-POP3Message -Client $Client -Index 6 -Path "$Env:UserProfile\Desktop\mail.eml"
Disconnect-POP3 -Client $Client