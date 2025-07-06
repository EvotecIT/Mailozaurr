using System.Collections.Generic;
using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphInboxRule"/> object.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphInboxRuleObject")]
[OutputType(typeof(GraphInboxRule))]
public sealed class CmdletNewGraphInboxRuleObject : PSCmdlet
{
    [Parameter(Mandatory = true)]
    public string DisplayName { get; set; } = string.Empty;

    [Parameter]
    public int Sequence { get; set; }

    [Parameter]
    public SwitchParameter Enabled { get; set; }

    [Parameter]
    public string? MoveToFolder { get; set; }

    [Parameter]
    public string[]? SenderContains { get; set; }

    protected override void ProcessRecord()
    {
        var rule = new GraphInboxRule
        {
            DisplayName = DisplayName,
            Sequence = Sequence,
            IsEnabled = Enabled.IsPresent
        };
        if (SenderContains != null && SenderContains.Length > 0)
        {
            rule.Conditions = new GraphInboxRulePredicates { SenderContains = new List<string>(SenderContains) };
        }
        if (!string.IsNullOrEmpty(MoveToFolder))
        {
            rule.Actions = new GraphInboxRuleActions { MoveToFolder = MoveToFolder };
        }
        WriteObject(rule);
    }
}
