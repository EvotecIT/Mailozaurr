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

    /// <summary>
    /// Use the Amazon SES REST API to deliver mail.
    /// </summary>
    SES,

    /// <summary>
    /// Use the Gmail REST API to deliver mail.
    /// </summary>
    Gmail,
    //MailChimp,
    //Moosend,
    //Postmark,
    //Brevo (Sendinblue),
    //MessageBird (SparkPost),
    //MailerSend
}

