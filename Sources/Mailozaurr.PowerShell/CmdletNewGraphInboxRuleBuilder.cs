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
    [Parameter(Mandatory = true)]
    public string DisplayName { get; set; } = string.Empty;

    [Parameter]
    public int Sequence { get; set; }

    [Parameter]
    public SwitchParameter Enabled { get; set; }

    [Parameter]
    public string? MoveToFolder { get; set; }

    [Parameter]
    public string? CopyToFolder { get; set; }

    [Parameter]
    public SwitchParameter Delete { get; set; }

    [Parameter]
    public string[]? ForwardTo { get; set; }

    [Parameter]
    public SwitchParameter StopProcessing { get; set; }

    [Parameter]
    public string[]? SenderContains { get; set; }

    [Parameter]
    public string[]? RecipientContains { get; set; }

    [Parameter]
    public string[]? SubjectContains { get; set; }

    [Parameter]
    public string[]? BodyContains { get; set; }

    [Parameter]
    public string? Importance { get; set; }

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
            builder.Importance(Importance);
        if (!string.IsNullOrEmpty(MoveToFolder))
            builder.MoveToFolder(MoveToFolder);
        if (!string.IsNullOrEmpty(CopyToFolder))
            builder.CopyToFolder(CopyToFolder);
        if (Delete.IsPresent)
            builder.Delete();
        if (ForwardTo != null)
            builder.ForwardTo(ForwardTo);
        if (StopProcessing.IsPresent)
            builder.StopProcessingRules();

        WriteObject(builder);
    }
}
