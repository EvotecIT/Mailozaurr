using MailKit.Net.Imap;
using MailKit.Security;
using Mailozaurr;
using System.Security;
using System.Security.Authentication;
using System.Threading;

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
///   <code>$clientSecret = Read-Host "Google client secret" -AsSecureString
/// $cred = Connect-OAuthGoogle -GmailAccount "user@gmail.com" -ClientID "id" -ClientSecretSecureString $clientSecret
/// Connect-IMAP -Server "imap.gmail.com" -Credential $cred -OAuth2</code>
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
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// <para type="description">Specifies the port to use for the IMAP connection. Default is 993 (IMAPS).</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int Port { get; set; } = 993;

    /// <summary>
    /// <para type="description">Skips certificate revocation checks during the connection. Useful for environments with limited certificate infrastructure.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SwitchParameter SkipCertificateRevocation { get; set; }

    /// <summary>
    /// <para type="description">Skips certificate validation. Use with caution; only for trusted/test environments or self-signed certificates.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SwitchParameter SkipCertificateValidation { get; set; }

    /// <summary>
    /// <para type="description">Specifies the username for clear text authentication. Required for the ClearText parameter set.</para>
    /// </summary>
    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// <para type="description">Specifies the password for clear text authentication. Required for the ClearText parameter set.</para>
    /// </summary>
    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// <para type="description">Specifies a PSCredential object for authentication. Used for OAuth2 or standard credential-based authentication.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// <para type="description">Specifies the secure socket options for the IMAP connection. Default is Auto. Options: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SecureSocketOptions Options { get; set; } = SecureSocketOptions.Auto;

    /// <summary>
    /// <para type="description">Specifies the connection timeout in milliseconds. Default is 120000 (2 minutes).</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int TimeOut { get; set; } = 120000;

    /// <summary>
    /// <para type="description">Number of connection retry attempts.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// <para type="description">Delay in milliseconds between retries.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// <para type="description">Multiplier for increasing retry delay.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public double RetryDelayBackoff { get; set; } = 1.0;

    /// <summary>
    /// <para type="description">Enables OAuth2 authentication. Use with a PSCredential object containing the access token as the password.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    public SwitchParameter OAuth2 { get; set; }

    /// <summary>
    /// Connects to the IMAP server and returns an <see cref="ImapConnectionInfo"/> object with connection and client details.
    /// </summary>
    /// <remarks>
    /// Use the returned object with <c>Disconnect-IMAP</c>, <c>Get-IMAPFolder</c>, or <c>Get-IMAPMessage</c> for further operations.
    /// </remarks>
    protected override async Task ProcessRecordAsync() {
        async Task Authenticate(ImapClient c, CancellationToken ct) {
            if (ParameterSetName == "OAuth2" && OAuth2.IsPresent) {
                if (Credential == null) {
                    throw new System.Security.Authentication.AuthenticationException("Credential is required for OAuth2 authentication.");
                }
                var username = Credential.UserName;
                var token = new System.Net.NetworkCredential(string.Empty, Credential.Password).Password;
                var sasl = new MailKit.Security.SaslMechanismOAuth2(username, token);
                await c.AuthenticateAsync(sasl, ct);
            } else if (ParameterSetName == "ClearText" && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password)) {
                await c.AuthenticateAsync(UserName, Password, ct);
            } else if (Credential != null) {
                var username = Credential.UserName;
                var password = Credential.Password is SecureString ss ? new System.Net.NetworkCredential(string.Empty, ss).Password : Credential.GetNetworkCredential().Password;
                await c.AuthenticateAsync(username, password, ct);
            } else {
                throw new System.Security.Authentication.AuthenticationException("No valid authentication method provided.");
            }
        }

        ImapClient client;
        try {
            client = await ImapConnector.ConnectAsync(
                Server,
                Port,
                Options,
                TimeOut,
                SkipCertificateRevocation.IsPresent,
                SkipCertificateValidation.IsPresent,
                Authenticate,
                RetryCount,
                RetryDelayMilliseconds,
                RetryDelayBackoff,
                CancelToken);
        } catch (Exception ex) {
            WriteWarning($"Connect-IMAP - {ex.Message}");
            return;
        }

        if (client.IsAuthenticated) {
            // Open the inbox once and cache folder reference
            try {
                _ = client.GetCachedFolder(null, MailKit.FolderAccess.ReadOnly);
            } catch (ImapCommandException ex) {
                var responseText = ex.ResponseText;
                if (!string.IsNullOrWhiteSpace(responseText)) {
                    LoggingMessages.Logger.WriteWarning($"Connect-IMAP - Failed to open inbox: {ex.Message} | Server response: {responseText}");
                } else {
                    LoggingMessages.Logger.WriteWarning($"Connect-IMAP - Failed to open inbox: {ex.Message}");
                }
            }
            var folder = client.GetCachedFolder(null, MailKit.FolderAccess.ReadOnly);
            if (folder is not ImapFolder inbox) {
                WriteWarning("Connect-IMAP - Inbox folder not found.");
                await client.DisconnectAsync(true);
                return;
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
                Count = inbox.Count,
                Messages = inbox,
                Recent = inbox.Recent,
                Folder = inbox
            };
            info.Folders[inbox.FullName] = inbox;
            DefaultSessions.ImapSession = info;
            WriteObject(info);
        } else {
            WriteWarning("Connect-IMAP - Authentication failed.");
            await client.DisconnectAsync(true);
        }
    }
}