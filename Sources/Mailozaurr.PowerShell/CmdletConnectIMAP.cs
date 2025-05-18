using MailKit.Net.Imap;
using MailKit.Security;
using System.Security;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Connects to an IMAP server and authenticates using various methods.</para>
/// <para type="description">This cmdlet connects to an IMAP server using MailKit and returns connection details and the client object for further use.</para>
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "IMAP")]
public sealed class CmdletConnectIMAP : AsyncPSCmdlet {
    // Parameter sets: oAuth2, Credential, ClearText
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true)]
    public string Server { get; set; }

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int Port { get; set; } = 993;

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SwitchParameter SkipCertificateRevocation { get; set; }

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SwitchParameter SkipCertificateValidation { get; set; }

    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    public string UserName { get; set; }

    [Parameter(ParameterSetName = "ClearText", Mandatory = true)]
    public string Password { get; set; }

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    public PSCredential Credential { get; set; }

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SecureSocketOptions Options { get; set; } = SecureSocketOptions.Auto;

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int TimeOut { get; set; } = 120000;

    [Parameter(ParameterSetName = "oAuth2")]
    public SwitchParameter oAuth2 { get; set; }

    /// <summary>
    /// Connects to the IMAP server and returns connection info.
    /// </summary>
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
                    // oAuth2 authentication (user must provide a valid SASL mechanism)
                    throw new NotImplementedException("oAuth2 authentication is not implemented in this cmdlet. Use a SASL mechanism.");
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
            } catch { /* ignore if fails */ }
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
            WriteObject(info);
        } else {
            WriteWarning("Connect-IMAP - Authentication failed.");
            await client.DisconnectAsync(true);
        }
    }
}