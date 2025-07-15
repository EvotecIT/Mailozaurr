using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Display name for the inbox rule.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Params")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Builder object used to create the rule.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Builder")]
    public GraphInboxRuleBuilder? Builder { get; set; }

    /// <summary>
    /// Rule processing sequence number.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public int Sequence { get; set; }

    /// <summary>
    /// Enables the rule when set.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public SwitchParameter Enabled { get; set; }

    /// <summary>
    /// Folder to move matching messages to.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string? MoveToFolder { get; set; }

    /// <summary>
    /// Folder to copy matching messages to.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string? CopyToFolder { get; set; }

    /// <summary>
    /// Deletes matching messages.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public SwitchParameter Delete { get; set; }

    /// <summary>
    /// Addresses to forward matching messages to.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string[]? ForwardTo { get; set; }

    /// <summary>
    /// Stops processing additional rules when this rule matches.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public SwitchParameter StopProcessing { get; set; }

    /// <summary>
    /// Sender address patterns to match.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string[]? SenderContains { get; set; }

    /// <summary>
    /// Recipient address patterns to match.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string[]? RecipientContains { get; set; }

    /// <summary>
    /// Subject text patterns to match.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string[]? SubjectContains { get; set; }

    /// <summary>
    /// Body text patterns to match.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string[]? BodyContains { get; set; }

    /// <summary>
    /// Importance level to match.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string? Importance { get; set; }

    /// <summary>
    /// Creates a <see cref="GraphInboxRule"/> instance from the supplied parameters.
    /// </summary>
    protected override void ProcessRecord()
    {
        if (ParameterSetName == "Builder")
        {
            WriteObject(Builder!.Build());
            return;
        }

        var rule = new GraphInboxRule
        {
            DisplayName = DisplayName,
            Sequence = Sequence,
            IsEnabled = Enabled.IsPresent
        };

        if (SenderContains != null || RecipientContains != null || SubjectContains != null || BodyContains != null || Importance != null)
        {
            rule.Conditions = new GraphInboxRulePredicates
            {
                SenderContains = SenderContains != null ? new List<string>(SenderContains) : null,
                RecipientContains = RecipientContains != null ? new List<string>(RecipientContains) : null,
                SubjectContains = SubjectContains != null ? new List<string>(SubjectContains) : null,
                BodyContains = BodyContains != null ? new List<string>(BodyContains) : null,
                Importance = Importance
            };
        }

        if (!string.IsNullOrEmpty(MoveToFolder) || !string.IsNullOrEmpty(CopyToFolder) || Delete.IsPresent || ForwardTo != null || StopProcessing.IsPresent)
        {
            rule.Actions = new GraphInboxRuleActions
            {
                MoveToFolder = MoveToFolder,
                CopyToFolder = CopyToFolder,
                Delete = Delete.IsPresent ? true : null,
                ForwardTo = ForwardTo != null ? new List<GraphEmailAddress>(ForwardTo.Select(a => new GraphEmailAddress { Email = new GraphEmail { Address = a } })) : null,
                StopProcessingRules = StopProcessing.IsPresent ? true : null
            };
        }

        WriteObject(rule);
    }
}
