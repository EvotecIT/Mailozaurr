Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Custom attachment built from memory
$bytes = [System.Text.Encoding]::UTF8.GetBytes('Hello from memory')
$memoryStream = [System.IO.MemoryStream]::new($bytes)
$mimePart = [MimeKit.MimePart]::new('text/plain')
$mimePart.Content = [MimeKit.MimeContent]::new($memoryStream)
$mimePart.FileName = 'Memory.txt'

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' \
    -Server 'smtp.example.com' -Port 25 -Subject 'Attachment Demo' -Body 'Check attachments' \
    -Attachment 'C:\\Temp\\report.pdf', $mimePart \
    -InlineAttachment 'C:\\Temp\\logo.png' -WhatIf -Verbose
