namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="description">Specifies the protocol used for mailbox operations.</para>
/// </summary>
public enum EmailProtocol
{
    /// <summary>
    /// <para type="description">Use the IMAP protocol.</para>
    /// </summary>
    Imap,

    /// <summary>
    /// <para type="description">Use the POP3 protocol.</para>
    /// </summary>
    Pop3,

    /// <summary>
    /// <para type="description">Use Microsoft Graph.</para>
    /// </summary>
    Graph
}
