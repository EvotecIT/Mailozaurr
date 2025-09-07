$pending = Join-Path $PSScriptRoot 'pending.log'

# Review queued messages
Get-MailozaurrPendingMessage -PendingPath $pending

# Replay messages. Server details and credentials are taken from
# the pending log so each message is sent using its original
# configuration, allowing mixed servers in a single file.
Send-MailozaurrPendingMessage -PendingPath $pending

# Remove a specific message by ID
# $id = 'message-id'
# Remove-MailozaurrPendingMessage -PendingPath $pending -MessageId $id
