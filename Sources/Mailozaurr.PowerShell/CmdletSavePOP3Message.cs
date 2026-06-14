using Mailozaurr;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Saves a POP3 message to disk in either EML or MSG format.</para>
/// <para type="description">The <c>Save-POP3Message</c> cmdlet saves a message from a POP3 mailbox (using a <see cref="PopConnectionInfo"/> object from <c>Connect-POP3</c>) to disk at the specified path. Use this to archive, export, or process messages retrieved from a POP3 server. The message can be saved as an EML file or converted to MSG format.</para>
/// <example>
///   <summary>Save a POP3 message to a file</summary>
///   <code>
/// $client = Connect-POP3 ...; Save-POP3Message -Client $client -Index 0 -Path "C:\Mail\message.eml"
/// $client = Connect-POP3 ...; Save-POP3Message -Client $client -Index 0 -Path "C:\Mail\message.msg"
///   </code>
/// </example>
/// <remarks>
/// Use this cmdlet to export or archive messages for backup, migration, or compliance scenarios.
/// </remarks>
/// <seealso cref="CmdletConnectPOP3"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.Save, "POP3Message")]
public sealed class CmdletSavePOP3Message : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="PopConnectionInfo"/> object representing the active POP3 connection. This is the object returned by <c>Connect-POP3</c>.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the index of the message to save.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 1)]
    public int Index { get; set; }

    /// <summary>
    /// <para type="description">Specifies the path where the message will be saved.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 2)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }


    /// <summary>
    /// Saves the specified POP3 message to disk at the given path.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn != null && conn.Data != null) {
            if (Index < conn.Data.Count) {
                var message = conn.Data.GetMessage(Index);
                if (Path != null) {
                    var resolved = System.IO.Path.GetFullPath(Path);
                    var directory = System.IO.Path.GetDirectoryName(resolved);
                    if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory)) {
                        System.IO.Directory.CreateDirectory(directory);
                    }
                    if (resolved.EndsWith(".msg", System.StringComparison.OrdinalIgnoreCase)) {
                        var tempEml = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid()}.eml");
                        try {
                            message.WriteTo(tempEml);
                            EmailMessage.ConvertEmlToMsg(new System.IO.FileInfo(tempEml), new System.IO.FileInfo(resolved), true);
                        } finally {
                            if (System.IO.File.Exists(tempEml)) {
                                System.IO.File.Delete(tempEml);
                            }
                        }
                    } else {
                        message.WriteTo(resolved);
                    }
                }
            } else {
                WriteWarning($"Save-POP3Message - Index is out of range. Use index less than {conn.Data.Count}.");
            }
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Save-POP3Message - POP3 client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
        return Task.CompletedTask;
    }
}