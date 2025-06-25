using MailKit.Net.Imap;
using MailKit.Security;
using System.Security;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Connects to an IMAP server and authenticates using credentials, OAuth2, or clear text.</para>
/// <para type="description">The <c>Connect-IMAP</c> cmdlet establishes a connection to an IMAP server using MailKit. It supports multiple authentication methods, including OAuth2, PSCredential, and clear text username/password. The cmdlet returns an <see cref="ImapConnectionInfo"/> object containing connection details and the authenticated client for further use in subsequent cmdlets.</para>
/// <para type="description">Supports advanced options such as certificate validation skipping, custom timeouts, and secure socket options. Designed for secure, flexible, and scriptable IMAP connectivity in PowerShell automation scenarios.</para>
/// <example>
///   <summary>Connect to an IMAP server using credentials</summary>
///   <code>Connect-IMAP -Server "imap.example.com" -Credential (Get-Credential)</code>
/// </example>
/// <example>
///   <summary>Connect to Gmail IMAP using OAuth2</summary>
///   <code>$cred = Connect-OAuthGoogle -GmailAccount "user@gmail.com" -ClientID "id" -ClientSecret "secret"
/// Connect-IMAP -Server "imap.gmail.com" -Credential $cred -oAuth2</code>
/// </example>
/// <example>
///   <summary>Connect to an IMAP server with clear text username and password</summary>
///   <code>Connect-IMAP -Server "imap.example.com" -UserName "user" -Password "pass"</code>
/// </example>
/// <remarks>
/// For OAuth2, use the <c>Connect-OAuthGoogle</c> or <c>Connect-OAuthO365</c> cmdlets to obtain a credential object.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// <seealso cref="CmdletDisconnectIMAP"/>
/// <seealso cref="CmdletGetIMAPFolder"/>
/// <seealso cref="CmdletGetIMAPMessage"/>
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "IMAP")]
public sealed class CmdletConnectIMAP : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the IMAP server hostname or IP address to connect to.</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Server { get; set; }

    /// <summary>
    /// <para type="description">Specifies the port to use for the IMAP connection. Default is 993 (IMAPS).</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int Port { get; set; } = 993;

    /// <summary>
    /// <para type="description">Skips certificate revocation checks during the connection. Useful for environments with limited certificate infrastructure.</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SwitchParameter SkipCertificateRevocation { get; set; }

    /// <summary>
    /// <para type="description">Skips certificate validation. Use with caution; only for trusted/test environments or self-signed certificates.</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SwitchParameter SkipCertificateValidation { get; set; }

    /// <summary>
    /// <para type="description">Specifies the username for clear text authentication. Required for the ClearText parameter set.</para>
    /// </summary>
    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string UserName { get; set; }

    /// <summary>
    /// <para type="description">Specifies the password for clear text authentication. Required for the ClearText parameter set.</para>
    /// </summary>
    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Password { get; set; }

    /// <summary>
    /// <para type="description">Specifies a PSCredential object for authentication. Used for OAuth2 or standard credential-based authentication.</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    public PSCredential Credential { get; set; }

    /// <summary>
    /// <para type="description">Specifies the secure socket options for the IMAP connection. Default is Auto. Options: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable.</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SecureSocketOptions Options { get; set; } = SecureSocketOptions.Auto;

    /// <summary>
    /// <para type="description">Specifies the connection timeout in milliseconds. Default is 120000 (2 minutes).</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int TimeOut { get; set; } = 120000;

    /// <summary>
    /// <para type="description">Enables OAuth2 authentication. Use with a PSCredential object containing the access token as the password.</para>
    /// </summary>
    [Parameter(ParameterSetName = "oAuth2")]
    public SwitchParameter oAuth2 { get; set; }

    /// <summary>
    /// Connects to the IMAP server and returns an <see cref="ImapConnectionInfo"/> object with connection and client details.
    /// </summary>
    /// <remarks>
    /// Use the returned object with <c>Disconnect-IMAP</c>, <c>Get-IMAPFolder</c>, or <c>Get-IMAPMessage</c> for further operations.
    /// </remarks>
    protected override async Task ProcessRecordAsync() {
        var client = new ImapClient();
        try {
            await client.ConnectAsync(Server, Port, Options);
        } catch (Exception ex) {
            WriteWarning($"Connect-IMAP - Unable to connect: {ex.Message}");
            return;
        }

        if (SkipCertificateRevocation) {
            client.CheckCertificateRevocation = false;
        }
        if (SkipCertificateValidation) {
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        }
        if (client.Timeout != TimeOut) {
            client.Timeout = TimeOut;
        }

        if (client.IsConnected) {
            try {
                if (ParameterSetName == "oAuth2" && oAuth2.IsPresent) {
                    // oAuth2 authentication using SASL
                    var username = Credential.UserName;
                    var token = new System.Net.NetworkCredential("", Credential.Password).Password;
                    var sasl = new MailKit.Security.SaslMechanismOAuth2(username, token);
                    await client.AuthenticateAsync(sasl);
                } else if (ParameterSetName == "ClearText" && !string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Password)) {
                    await client.AuthenticateAsync(UserName, Password);
                } else if (Credential != null) {
                    var username = Credential.UserName;
                    var password = Credential.Password is SecureString ss ? new System.Net.NetworkCredential("", ss).Password : Credential.GetNetworkCredential().Password;
                    await client.AuthenticateAsync(username, password);
                } else {
                    WriteWarning("Connect-IMAP - No valid authentication method provided.");
                    await client.DisconnectAsync(true);
                    return;
                }
            } catch (Exception ex) {
                WriteWarning($"Connect-IMAP - Unable to authenticate: {ex.Message}");
                await client.DisconnectAsync(true);
                return;
            }
        } else {
            WriteWarning("Connect-IMAP - Client is not connected after ConnectAsync.");
            return;
        }

        if (client.IsAuthenticated) {
            // Open the inbox to get message info
            try {
                await client.Inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning($"Connect-IMAP - Failed to open inbox: {ex.Message}");
            }
            var info = new ImapConnectionInfo {
                Uri = $"imaps://{Server}:{Port}/",
                AuthenticationMechanisms = client.AuthenticationMechanisms,
                Capabilities = client.Capabilities,
                Stream = null, // Not exposed
                State = null, // Not exposed
                IsConnected = client.IsConnected,
                ApopToken = null, // Not applicable for IMAP
                ExpirePolicy = null, // Not applicable for IMAP
                Implementation = null, // Not directly available
                LoginDelay = null, // Not directly available
                IsAuthenticated = client.IsAuthenticated,
                IsSecure = client.IsSecure,
                Data = client,
                Count = client.Inbox?.Count ?? 0,
                Messages = client.Inbox,
                Recent = client.Inbox?.Recent ?? 0
            };
            DefaultSessions.ImapSession = info;
            WriteObject(info);
        } else {
            WriteWarning("Connect-IMAP - Authentication failed.");
            await client.DisconnectAsync(true);
        }
    }
}