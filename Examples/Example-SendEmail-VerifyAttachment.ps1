Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$missing = "$PSScriptRoot\missing-file.txt"

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' \
    -Server 'smtp.example.com' -Port 25 -Subject 'Verify attachments demo' \
    -Body 'demo body' -Attachment $missing -WhatIf -Verbose
