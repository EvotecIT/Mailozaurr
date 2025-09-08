$pending = Join-Path $PSScriptRoot 'pending'
$sent = Join-Path $PSScriptRoot 'sentlog.json'
$smtp = [Mailozaurr.Smtp]::new()
$smtp.Server = 'smtp.server'
$smtp.PendingMessagesPath = $pending
$smtp.SentMessageRepository = [Mailozaurr.FileSentMessageRepository]::new($sent)
$smtp.ProcessPendingMessagesAsync().GetAwaiter().GetResult()

