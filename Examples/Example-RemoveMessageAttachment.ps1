Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Create a MIME message and strip attachments before forwarding
$path = Join-Path $PSScriptRoot 'sample.txt'
'content' | Set-Content -Path $path
$msg = New-MimeMessage -From 'Sender <sender@example.com>' -To 'Recipient <recipient@example.com>' -Subject 'Sample' -TextBody 'Forwarding without attachments' -AttachmentPath $path

$msg = Remove-IMAPMessageAttachment -Message $msg
Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Subject $msg.Subject -Body 'Forwarding without attachments' -Server 'smtp.example.com' -Port 25 -Message $msg -WhatIf

# For Graph messages, pipe an existing draft or retrieved Graph message object
# through Remove-GraphMessageAttachment before forwarding it with your preferred method.
# $graphMsg = $graphMsg | Remove-GraphMessageAttachment

# For POP3 messages (after retrieval)
$popMsg = New-MimeMessage -TextBody 'Forwarding without attachments' -AttachmentPath $path
$popMsg = Remove-POP3MessageAttachment -Message $popMsg
