namespace Mailozaurr;

public class ClientSmtp : SmtpClient {
    public List<string> DeliveryNotificationOption { get; set; } = new List<string>();

    public ClientSmtp() { }

    public ClientSmtp(ProtocolLogger protocolLogger) : base(protocolLogger) { }

    protected override DeliveryStatusNotification? GetDeliveryStatusNotifications(MimeMessage message, MailboxAddress mailbox) {
        var output = new List<DeliveryStatusNotification>();

        if (DeliveryNotificationOption.Contains("OnSuccess")) {
            output.Add(DeliveryStatusNotification.Success);
        }
        if (DeliveryNotificationOption.Contains("Delay")) {
            output.Add(DeliveryStatusNotification.Delay);
        }
        if (DeliveryNotificationOption.Contains("OnFailure")) {
            output.Add(DeliveryStatusNotification.Failure);
        }
        if (DeliveryNotificationOption.Contains("Never")) {
            output.Add(DeliveryStatusNotification.Never);
        }

        return output.Count > 0 ? (DeliveryStatusNotification?)output[0] : null;
    }
}
