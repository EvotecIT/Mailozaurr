$pending = Join-Path $PSScriptRoot 'pending'
$adminRecipient = 'admin@example.com'
$notificationSender = 'mailer@example.com'
$smtpServer = 'smtp.office365.com'
$notificationSubject = 'Mailozaurr pending message queue failure'

if (-not $NotificationCredential) {
    $NotificationCredential = Get-Credential -Message 'Provide SMTP credentials used for failure notifications'
}

$notificationParameters = @{
    From       = $notificationSender
    To         = $adminRecipient
    Subject    = $notificationSubject
    SmtpServer = $smtpServer
    Credential = $NotificationCredential
    UseSsl     = $true
    Port       = 587
}

try {
    Send-EmailPendingMessage -PendingMessagesPath $pending -ProcessAll
} catch {
    $timestamp = Get-Date -Format o
    $body = @"
Processing of the pending message queue failed at $timestamp.

$($_.Exception.Message)
"@
    Send-EmailMessage @notificationParameters -Body $body
    throw
}

# To automatically process the queue, register a scheduled job that reuses the
# same notification block. The example below runs every 15 minutes and alerts the
# administrator when a failure occurs.
#
# $trigger = New-JobTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 15) -RepetitionDuration ([TimeSpan]::MaxValue)
# Register-ScheduledJob -Name 'Mailozaurr-PendingMessages' -ScriptBlock {
#     param(
#         [string]$PendingMessagesPath,
#         [hashtable]$NotificationParameters
#     )
#
#     try {
#         Send-EmailPendingMessage -PendingMessagesPath $PendingMessagesPath -ProcessAll
#     } catch {
#         $errorTimestamp = Get-Date -Format o
#         $errorBody = @"
# Processing of the pending message queue failed at $errorTimestamp.
#
# $($_.Exception.Message)
# "@
#         Send-EmailMessage @NotificationParameters -Body $errorBody
#         throw
#     }
# } -ArgumentList $pending, $notificationParameters -Trigger $trigger -ScheduledJobOption (New-ScheduledJobOption -RunElevated)
