Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$UserName = 'email@gmail.com'
$Password = ''

$Client = Connect-IMAP -Server 'imap.gmail.com' -Password $Password -UserName $UserName -Port 993 -Options Auto

Get-IMAPFolder -Client $Client -Verbose
Get-IMAPMessage -Client $Client -FromContains 'contoso.com' -HasAttachment

Disconnect-IMAP -Client $Client