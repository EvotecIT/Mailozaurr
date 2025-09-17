$pending = Join-Path $PSScriptRoot 'pending'

# Run once to flush all messages that are due right now.
Send-EmailPendingMessage -PendingMessagesPath $pending

# To process the queue from a scheduled task, register a job that calls the cmdlet.
# The example below runs every 15 minutes and forces processing of all messages
# (even those scheduled for the future) so that long-lived queues are cleared.
#
# $trigger = New-JobTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 15) -RepetitionDuration ([TimeSpan]::MaxValue)
# Register-ScheduledJob -Name 'Mailozaurr-PendingMessages' -ScriptBlock {
#     Send-EmailPendingMessage -PendingMessagesPath '$pending' -ProcessAll
# } -Trigger $trigger -ScheduledJobOption (New-ScheduledJobOption -RunElevated)

