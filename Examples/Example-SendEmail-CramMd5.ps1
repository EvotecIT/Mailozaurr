Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential

Send-EmailMessage -From $cred.UserName -To 'recipient@example.com' -Server 'smtp.example.com' \
    -Port 587 -Username $cred.UserName -Password $cred.GetNetworkCredential().Password \
    -AuthenticationMechanism CramMd5 -Verbose -WhatIf
