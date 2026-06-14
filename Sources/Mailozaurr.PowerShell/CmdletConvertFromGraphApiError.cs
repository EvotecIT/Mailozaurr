using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Parses a Microsoft Graph HTTP error transcript into a structured object.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "GraphApiError")]
[OutputType(typeof(GraphApiError))]
public sealed class CmdletConvertFromGraphApiError : PSCmdlet {
    /// <summary>Raw Graph HTTP response or error text.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public string? InputObject { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        WriteObject(GraphApiErrorParser.Parse(InputObject!));
    }
}
