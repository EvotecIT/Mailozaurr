using Mailozaurr;
using System;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphEventBuilder"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphEventBuilder")]
[OutputType(typeof(GraphEventBuilder))]
public sealed class CmdletNewGraphEventBuilder : PSCmdlet {
    /// <summary>
    /// Subject of the new event.
    /// </summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>
    /// Start time of the event.
    /// </summary>
    [Parameter]
    public DateTime? Start { get; set; }

    /// <summary>
    /// Time zone for the start time.
    /// </summary>
    [Parameter]
    public string StartTimeZone { get; set; } = "UTC";

    /// <summary>
    /// End time of the event.
    /// </summary>
    [Parameter]
    public DateTime? End { get; set; }

    /// <summary>
    /// Time zone for the end time.
    /// </summary>
    [Parameter]
    public string EndTimeZone { get; set; } = "UTC";

    /// <summary>
    /// Body content of the event.
    /// </summary>
    [Parameter]
    public string? Body { get; set; }

    /// <summary>
    /// Type of the body content: HTML or Text.
    /// </summary>
    [Parameter]
    public string BodyType { get; set; } = "HTML";

    /// <summary>
    /// List of attendee email addresses.
    /// </summary>
    [Parameter]
    public string[]? Attendees { get; set; }

    /// <summary>
    /// Builds the <see cref="GraphEventBuilder"/> object from provided parameters.
    /// </summary>
    protected override void ProcessRecord() {
        var builder = new GraphEventBuilder();
        if (!string.IsNullOrEmpty(Subject))
            builder.Subject(Subject!);
        if (Start.HasValue)
            builder.Start(Start.Value, StartTimeZone);
        if (End.HasValue)
            builder.End(End.Value, EndTimeZone);
        if (!string.IsNullOrEmpty(Body))
            builder.Body(Body!, BodyType);
        if (Attendees != null) {
            foreach (var addr in Attendees) {
                builder.Attendee(addr, addr);
            }
        }
        WriteObject(builder);
    }
}