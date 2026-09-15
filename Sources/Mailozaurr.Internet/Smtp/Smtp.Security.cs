namespace Mailozaurr;

public partial class Smtp {
    /// <summary>
    /// Gets the provider-neutral security mode used by the active or most recent connection attempt.
    /// </summary>
    public SmtpSecurityMode ActiveSecurityMode => ActiveSecureSocketOptions.ToSmtpSecurityMode();
}
