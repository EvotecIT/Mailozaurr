Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Custom attachment built from memory
$bytes = [System.Text.Encoding]::UTF8.GetBytes('Hello from memory')
$attachment = New-EmailAttachment -Bytes $bytes -FileName 'Memory.txt' -ContentType 'text/plain'

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' \
    -Server 'smtp.example.com' -Port 25 -Subject 'Attachment Demo' -Body 'Check attachments' \
    -Attachment 'C:\\Temp\\report.pdf', $attachment \
    -InlineAttachment 'C:\\Temp\\logo.png' -WhatIf -Verbose
