$path = Join-Path $PSScriptRoot 'sentlog.json'
# The file collects all sent message records and is appended to on each send.
Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Server 'smtp.server' -Subject 'demo' -Text 'body' -SentLogPath $path -WhatIf
# $ndr = Get-NonDeliveryReport -Path 'ndr.eml'
# $match = Get-EmailDeliveryMatch -Protocol Imap -SentLogPath $path -MessageId $ndr.MessageId
# $match | Format-List
