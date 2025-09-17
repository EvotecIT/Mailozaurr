$pending = Join-Path $PSScriptRoot 'pending'

# Review queued messages
Get-EmailPendingMessage -PendingMessagesPath $pending

# Replay messages. Server details and credentials are taken from
# the pending log so each message is sent using its original
# configuration, allowing mixed servers in a single file.
Send-EmailPendingMessage -PendingMessagesPath $pending

# Replay a specific message (and optionally restrict to a provider)
# $id = 'message-id'
# Send-EmailPendingMessage -PendingMessagesPath $pending -MessageId $id -Provider None

# Remove a specific message by ID
# Remove-EmailPendingMessage -PendingMessagesPath $pending -MessageId $id
