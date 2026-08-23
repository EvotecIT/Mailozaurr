using System;
using System.Collections.Generic;

namespace Mailozaurr;

/// <summary>
/// Fluent builder for <see cref="GraphEvent"/> objects.
/// </summary>
public sealed class GraphEventBuilder {
    private readonly GraphEvent _event = new();

    /// <summary>Sets the subject.</summary>
    public GraphEventBuilder Subject(string subject) {
        _event.Subject = subject;
        return this;
    }

    /// <summary>Sets the start time.</summary>
    public GraphEventBuilder Start(DateTime dateTime, string timeZone = "UTC") {
        _event.Start = new GraphEventTime { DateTime = dateTime.ToString("o"), TimeZone = timeZone };
        return this;
    }

    /// <summary>Sets the end time.</summary>
    public GraphEventBuilder End(DateTime dateTime, string timeZone = "UTC") {
        _event.End = new GraphEventTime { DateTime = dateTime.ToString("o"), TimeZone = timeZone };
        return this;
    }

    /// <summary>Sets the body content.</summary>
    public GraphEventBuilder Body(string content, string type = "HTML") {
        _event.Body = new GraphContent { Content = content, Type = type };
        return this;
    }

    /// <summary>Adds an attendee.</summary>
    public GraphEventBuilder Attendee(string address, string name, string type = "required") {
        _event.Attendees ??= new List<GraphEventAttendee>();
        _event.Attendees.Add(new GraphEventAttendee {
            EmailAddress = new GraphEmail { Address = address, Name = name },
            Type = type
        });
        return this;
    }

    /// <summary>Gets the constructed <see cref="GraphEvent"/> instance.</summary>
    internal GraphEvent Build() => _event;

    /// <summary>
    /// Allows <see cref="GraphEventBuilder"/> to be used wherever
    /// <see cref="GraphEvent"/> is expected.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    public static implicit operator GraphEvent(GraphEventBuilder builder) => builder._event;
}
