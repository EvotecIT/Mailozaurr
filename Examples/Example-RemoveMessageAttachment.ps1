Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Create a MIME message and strip attachments before forwarding
$builder = [MimeKit.BodyBuilder]::new()
$builder.TextBody = 'Forwarding without attachments'
$path = Join-Path $PSScriptRoot 'sample.txt'
'content' | Set-Content -Path $path
$builder.Attachments.Add($path) | Out-Null
$msg = [MimeKit.MimeMessage]::new()
$msg.From.Add([MimeKit.MailboxAddress]::new('Sender','sender@example.com'))
$msg.To.Add([MimeKit.MailboxAddress]::new('Recipient','recipient@example.com'))
$msg.Subject = 'Sample'
$msg.Body = $builder.ToMessageBody()

$msg = Remove-MessageAttachment -MimeMessage $msg
Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Subject $msg.Subject -Body $builder.TextBody -Server 'smtp.example.com' -Port 25 -Message $msg -WhatIf

# For Graph messages
$file = Join-Path $PSScriptRoot 'sample2.txt'
'content2' | Set-Content -Path $file
$graphMsg = [Mailozaurr.GraphMessage]::new()
$graphMsg.Subject = 'Graph sample'
$graphMsg.Body = [Mailozaurr.GraphContent]::new()
$graphMsg.Attachments = @([Mailozaurr.GraphAttachment]::FromFile($file))
$graphMsg = Remove-MessageAttachment -GraphMessage $graphMsg
# Forward $graphMsg using your preferred method
