namespace Mailozaurr;
/// <summary>
/// Supported external email providers.
/// </summary>
public enum EmailProvider {
    /// <summary>
    /// Use the built-in SMTP client.
    /// </summary>
    None,

    /// <summary>
    /// Use the SendGrid REST API to deliver mail.
    /// </summary>
    SendGrid,

    /// <summary>
    /// Use the Mailgun REST API to deliver mail.
    /// </summary>
    Mailgun,
    //MailChimp,
    //AmazonSES,
    //Moosend,
    //Postmark,
    //Brevo (Sendinblue),
    //MessageBird (SparkPost),
    //MailerSend
}

