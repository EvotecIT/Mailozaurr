using System;
using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphEventBuilder"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphEventBuilder")]
[OutputType(typeof(GraphEventBuilder))]
public sealed class CmdletNewGraphEventBuilder : PSCmdlet
{
    [Parameter]
    public string? Subject { get; set; }

    [Parameter]
    public DateTime? Start { get; set; }

    [Parameter]
    public string StartTimeZone { get; set; } = "UTC";

    [Parameter]
    public DateTime? End { get; set; }

    [Parameter]
    public string EndTimeZone { get; set; } = "UTC";

    [Parameter]
    public string? Body { get; set; }

    [Parameter]
    public string BodyType { get; set; } = "HTML";

    [Parameter]
    public string[]? Attendees { get; set; }

    protected override void ProcessRecord()
    {
        var builder = new GraphEventBuilder();
        if (!string.IsNullOrEmpty(Subject))
            builder.Subject(Subject);
        if (Start.HasValue)
            builder.Start(Start.Value, StartTimeZone);
        if (End.HasValue)
            builder.End(End.Value, EndTimeZone);
        if (!string.IsNullOrEmpty(Body))
            builder.Body(Body, BodyType);
        if (Attendees != null)
        {
            foreach (var addr in Attendees)
            {
                builder.Attendee(addr, addr);
            }
        }
        WriteObject(builder);
    }
}
