using MailKit.Net.Pop3;
using MailKit.Security;
using System.Security;
using System.Security.Authentication;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Connects to a POP3 server and authenticates using credentials, OAuth2, or clear text.</para>
/// <para type="description">The <c>Connect-POP3</c> cmdlet establishes a connection to a POP3 server using MailKit. It supports multiple authentication methods, including OAuth2, PSCredential, and clear text username/password. The cmdlet returns a <see cref="PopConnectionInfo"/> object containing connection details and the authenticated client for further use in subsequent cmdlets.</para>
/// <para type="description">Supports advanced options such as certificate validation skipping, custom timeouts, and secure socket options. Designed for secure, flexible, and scriptable POP3 connectivity in PowerShell automation scenarios.</para>
/// <example>
///   <summary>Connect to a POP3 server using credentials</summary>
///   <code>Connect-POP3 -Server "pop.example.com" -Credential (Get-Credential)</code>
/// </example>
/// <example>
///   <summary>Connect to Gmail POP3 using OAuth2</summary>
///   <code>$cred = Connect-OAuthGoogle -GmailAccount "user@gmail.com" -ClientID "id" -ClientSecret "secret"
/// Connect-POP3 -Server "pop.gmail.com" -Credential $cred -OAuth2</code>
/// </example>
/// <example>
///   <summary>Connect to a POP3 server with clear text username and password</summary>
///   <code>Connect-POP3 -Server "pop.example.com" -UserName "user" -Password "pass"</code>
/// </example>
/// <remarks>
/// For OAuth2, use the <c>Connect-OAuthGoogle</c> or <c>Connect-OAuthO365</c> cmdlets to obtain a credential object.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// <seealso cref="CmdletDisconnectPOP3"/>
/// <seealso cref="CmdletGetPOP3Message"/>
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "POP3")]
public sealed class CmdletConnectPOP3 : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the POP3 server hostname or IP address to connect to.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Server { get; set; }

    /// <summary>
    /// <para type="description">Specifies the port to use for the POP3 connection. Default is 995 (POPS).</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int Port { get; set; } = 995;

    /// <summary>
    /// <para type="description">Specifies the username for clear text authentication. Required for the ClearText parameter set.</para>
    /// </summary>
    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserName { get; set; }

    /// <summary>
    /// <para type="description">Specifies the password for clear text authentication. Required for the ClearText parameter set.</para>
    /// </summary>
    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Password { get; set; }

    /// <summary>
    /// <para type="description">Specifies a PSCredential object for authentication. Used for OAuth2 or standard credential-based authentication.</para>
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    public PSCredential? Credential { get; set; }

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
    /// <para type="description">Specifies the secure socket options for the POP3 connection. Default is Auto. Options: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable.</para>
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
    /// Connects to the POP3 server and returns a <see cref="PopConnectionInfo"/> object with connection and client details.
    /// </summary>
    /// <remarks>
    /// Use the returned object with <c>Disconnect-POP3</c> or <c>Get-POP3Message</c> for further operations.
    /// </remarks>
    protected override async Task ProcessRecordAsync() {
        async Task Authenticate(Pop3Client c) {
            if (ParameterSetName == "OAuth2" && OAuth2.IsPresent) {
                var username = Credential.UserName;
                var token = new System.Net.NetworkCredential(string.Empty, Credential.Password).Password;
                var sasl = new MailKit.Security.SaslMechanismOAuth2(username, token);
                await c.AuthenticateAsync(sasl);
            } else if (ParameterSetName == "ClearText" && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password)) {
                await c.AuthenticateAsync(UserName, Password);
            } else if (Credential != null) {
                var username = Credential.UserName;
                var password = Credential.Password is SecureString ss ? new System.Net.NetworkCredential(string.Empty, ss).Password : Credential.GetNetworkCredential().Password;
                await c.AuthenticateAsync(username, password);
            } else {
                throw new System.Security.Authentication.AuthenticationException("No valid authentication method provided.");
            }
        }

        Pop3Client client;
        try {
            client = await Pop3Connector.ConnectAsync(
                Server,
                Port,
                Options,
                TimeOut,
                SkipCertificateRevocation.IsPresent,
                SkipCertificateValidation.IsPresent,
                Authenticate,
                RetryCount,
                RetryDelayMilliseconds,
                RetryDelayBackoff);
        } catch (Exception ex) {
            WriteWarning($"Connect-POP3 - {ex.Message}");
            return;
        }

        if (client.IsAuthenticated) {
            var info = new PopConnectionInfo {
                Uri = $"pops://{Server}:{Port}/",
                AuthenticationMechanisms = client.AuthenticationMechanisms,
                Capabilities = client.Capabilities,
                Stream = null, // Not exposed
                State = null, // Not exposed
                IsConnected = client.IsConnected,
                ApopToken = null, // Not directly available
                ExpirePolicy = null, // Not directly available
                Implementation = null, // Not directly available
                LoginDelay = null, // Not directly available
                IsAuthenticated = client.IsAuthenticated,
                IsSecure = client.IsSecure,
                Data = client,
                Count = client.Count,
                Messages = null, // Not directly available
                Recent = 0 // Not directly available
            };
            DefaultSessions.Pop3Session = info;
            WriteObject(info);
        } else {
            WriteWarning("Connect-POP3 - Authentication failed.");
            await client.DisconnectAsync(true);
        }
    }
}