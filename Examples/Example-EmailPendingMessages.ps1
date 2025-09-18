$pending = Join-Path $PSScriptRoot 'pending'

# Existing pending queues created prior to credential protection will still replay,
# and the entries will be re-encrypted with the new protector the next time they are
# saved by the module (for example, after another delivery attempt).

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
