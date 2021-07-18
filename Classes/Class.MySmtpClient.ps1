class MySmtpClient : MailKit.Net.Smtp.SmtpClient {
    MySmtpClient() : base() {}

    [string[]] $DeliveryNotificationOption = @()
    [Nullable[MailKit.DeliveryStatusNotification]] GetDeliveryStatusNotifications([MimeKit.MimeMessage]$message, [MimeKit.MailboxAddress]$mailbox) {
        $Output = @(
            if ($this.DeliveryNotificationOption -contains 'OnSuccess') {
                [MailKit.DeliveryStatusNotification]::Success
            }
            if ($this.DeliveryNotificationOption -contains 'Delay') {
                [MailKit.DeliveryStatusNotification]::Delay
            }
            if ($this.DeliveryNotificationOption -contains 'OnFailure') {
                [MailKit.DeliveryStatusNotification]::Failure
            }
            if ($this.DeliveryNotificationOption -contains 'Never') {
                [MailKit.DeliveryStatusNotification]::Never
            }
        )
        if ($Output.Count -gt 0) {
            return $Output
        }
        return $null
    }
}