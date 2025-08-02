$path = Join-Path $PSScriptRoot 'sentlog.json'
Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Server 'smtp.server' -Subject 'demo' -Text 'body' -SentLogPath $path -WhatIf
# $ndr = Get-NonDeliveryReport -Path 'ndr.eml'
# $repo = [Mailozaurr.FileSentMessageRepository]::new($path)
# $resolver = [Mailozaurr.SendLogResolver]::new($repo)
# $match = $resolver.ResolveAsync($ndr).GetAwaiter().GetResult()
# $match | Format-List
