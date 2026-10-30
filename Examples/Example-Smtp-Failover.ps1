Import-Module Mailozaurr

$DeliverySession = [Mailozaurr.SmtpDeliverySession]::new()
try {
    $EmailParameters = @{
        From                   = 'notifications@example.com'
        Server                 = 'smtp+starttls://relay-a.example.com:587', 'smtps://relay-b.example.com:465'
        Strategy               = 'Failover'
        DeliverySession        = $DeliverySession
        RetryCount             = 3
        RetryDelayMilliseconds = 500
        CircuitBreakMinutes    = 2
    }
    # Remove WhatIf when ready to send through your own configured relays.
    foreach ($Recipient in 'first@example.com', 'second@example.com') {
        Send-EmailMessage @EmailParameters -To $Recipient -Subject 'Notification' -Body 'Your notification' -WhatIf
    }
} finally {
    $DeliverySession.Dispose()
}
