using System.Linq;
using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphInboxRuleBuilder"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphInboxRuleBuilder")]
[OutputType(typeof(GraphInboxRuleBuilder))]
public sealed class CmdletNewGraphInboxRuleBuilder : PSCmdlet
{
    /// <summary>
    /// Display name for the inbox rule.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Rule processing order.
    /// </summary>
    [Parameter]
    public int Sequence { get; set; }

    /// <summary>
    /// Enables the rule when set.
    /// </summary>
    [Parameter]
    public SwitchParameter Enabled { get; set; }

    /// <summary>
    /// Destination folder to move messages to.
    /// </summary>
    [Parameter]
    public string? MoveToFolder { get; set; }

    /// <summary>
    /// Destination folder to copy messages to.
    /// </summary>
    [Parameter]
    public string? CopyToFolder { get; set; }

    /// <summary>
    /// Deletes messages that match the rule.
    /// </summary>
    [Parameter]
    public SwitchParameter Delete { get; set; }

    /// <summary>
    /// Addresses to forward matching messages to.
    /// </summary>
    [Parameter]
    public string[]? ForwardTo { get; set; }

    /// <summary>
    /// Stops processing further rules when this rule matches.
    /// </summary>
    [Parameter]
    public new SwitchParameter StopProcessing { get; set; }

    /// <summary>
    /// Sender address patterns to match.
    /// </summary>
    [Parameter]
    public string[]? SenderContains { get; set; }

    /// <summary>
    /// Recipient address patterns to match.
    /// </summary>
    [Parameter]
    public string[]? RecipientContains { get; set; }

    /// <summary>
    /// Subject text patterns to match.
    /// </summary>
    [Parameter]
    public string[]? SubjectContains { get; set; }

    /// <summary>
    /// Body text patterns to match.
    /// </summary>
    [Parameter]
    public string[]? BodyContains { get; set; }

    /// <summary>
    /// Message importance to match.
    /// </summary>
    [Parameter]
    public string? Importance { get; set; }

    /// <summary>
    /// Builds the <see cref="GraphInboxRule"/> based on provided parameters.
    /// </summary>
    protected override void ProcessRecord()
    {
        var builder = new GraphInboxRuleBuilder()
            .DisplayName(DisplayName)
            .Sequence(Sequence)
            .Enabled(Enabled.IsPresent);

        if (SenderContains != null)
            builder.SenderContains(SenderContains!);
        if (RecipientContains != null)
            builder.RecipientContains(RecipientContains!);
        if (SubjectContains != null)
            builder.SubjectContains(SubjectContains!);
        if (BodyContains != null)
            builder.BodyContains(BodyContains!);
        if (!string.IsNullOrEmpty(Importance))
            builder.Importance(Importance!);
        if (!string.IsNullOrEmpty(MoveToFolder))
            builder.MoveToFolder(MoveToFolder!);
        if (!string.IsNullOrEmpty(CopyToFolder))
            builder.CopyToFolder(CopyToFolder!);
        if (Delete.IsPresent)
            builder.Delete();
        if (ForwardTo != null)
            builder.ForwardTo(ForwardTo);
        if (StopProcessing.IsPresent)
            builder.StopProcessingRules();

        WriteObject(builder);
    }
}
