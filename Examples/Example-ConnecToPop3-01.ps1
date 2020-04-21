Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$UserName = 'Test@gmail.com'
$Password = 'TextPassword'

$Client = Connect-POP3 -Server 'pop.gmail.com' -Password $Password -UserName $UserName -Port 995 -Options Auto
Get-POP3Message -Client $Client -Index 0 -Count 5
Save-POP3Message -Client $Client -Index 6 -Path "$Env:UserProfile\Desktop\mail.eml"
Disconnect-POP3 -Client $Client