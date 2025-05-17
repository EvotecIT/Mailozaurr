using System;
using System.Management.Automation;
using System.Threading.Tasks;
using MailKit.Net.Pop3;
using MailKit.Security;
using System.Security;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Connects to a POP3 server and authenticates using various methods.</para>
/// <para type="description">This cmdlet connects to a POP3 server using MailKit and returns connection details and the client object for further use.</para>
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "POP")]
[Alias("Connect-POP3")]
public sealed class CmdletConnectPOP : AsyncPSCmdlet {
    // Parameter sets: oAuth2, Credential, ClearText
    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    [Parameter(Mandatory = true)]
    public string Server { get; set; }

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public int Port { get; set; } = 995;

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
    public SwitchParameter SkipCertificateRevocation { get; set; }

    [Parameter(ParameterSetName = "oAuth2")]
    [Parameter(ParameterSetName = "Credential")]
    [Parameter(ParameterSetName = "ClearText")]
    public SwitchParameter SkipCertificateValidation { get; set; }

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
    /// Connects to the POP3 server and returns connection info.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var client = new Pop3Client();
        try {
            await client.ConnectAsync(Server, Port, Options);
        } catch (Exception ex) {
            WriteWarning($"Connect-POP - Unable to connect: {ex.Message}");
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
                    WriteWarning("Connect-POP - No valid authentication method provided.");
                    await client.DisconnectAsync(true);
                    return;
                }
            } catch (Exception ex) {
                WriteWarning($"Connect-POP - Unable to authenticate: {ex.Message}");
                await client.DisconnectAsync(true);
                return;
            }
        } else {
            WriteWarning("Connect-POP - Client is not connected after ConnectAsync.");
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
            WriteObject(info);
        } else {
            WriteWarning("Connect-POP - Authentication failed.");
            await client.DisconnectAsync(true);
        }
    }
}