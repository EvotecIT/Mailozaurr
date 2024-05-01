namespace Mailozaurr;

public class ClientSmtp : SmtpClient {
    public List<DeliveryStatusNotification> DeliveryNotificationOption { get; set; } = new List<DeliveryStatusNotification>();

    public ClientSmtp() { }

    public ClientSmtp(ProtocolLogger protocolLogger) : base(protocolLogger) { }

    protected override DeliveryStatusNotification? GetDeliveryStatusNotifications(MimeMessage message, MailboxAddress mailbox) {
        DeliveryStatusNotification combinedOption = 0;
        foreach (var option in DeliveryNotificationOption) {
            combinedOption |= option;
        }
        return combinedOption;
    }
}