using System.Globalization;
using System.Text.Json;
using OfficeIMO.Email;
using static Mailozaurr.MailboxExport.GraphMailboxExportSource;

namespace Mailozaurr.MailboxExport;

/// <summary>Maps Microsoft Graph contact, event and To Do task resources to typed Outlook items.</summary>
internal static class GraphMailboxItemMapper {
    internal static EmailDocument MapContact(JsonElement contact) {
        var item = new OutlookContact {
            DisplayName = GetString(contact, "displayName"),
            Prefix = GetString(contact, "title"),
            Initials = GetString(contact, "initials"),
            GivenName = GetString(contact, "givenName"),
            MiddleName = GetString(contact, "middleName"),
            Surname = GetString(contact, "surname"),
            Generation = GetString(contact, "generation"),
            NickName = GetString(contact, "nickName"),
            CompanyName = GetString(contact, "companyName"),
            JobTitle = GetString(contact, "jobTitle"),
            Department = GetString(contact, "department"),
            OfficeLocation = GetString(contact, "officeLocation"),
            Profession = GetString(contact, "profession"),
            ManagerName = GetString(contact, "manager"),
            AssistantName = GetString(contact, "assistantName"),
            SpouseName = GetString(contact, "spouseName"),
            FileAs = GetString(contact, "fileAs"),
            BusinessHomePage = GetString(contact, "businessHomePage"),
            Birthday = GetDateTimeOffset(contact, "birthday")
        };
        foreach (string child in GetStrings(contact, "children")) item.Children.Add(child);
        IReadOnlyList<string> imAddresses = GetStrings(contact, "imAddresses");
        if (imAddresses.Count > 0) item.InstantMessagingAddress = imAddresses[0];

        if (contact.TryGetProperty("emailAddresses", out JsonElement emails) && emails.ValueKind == JsonValueKind.Array) {
            OutlookContactEmailAddress[] slots = { item.Email1, item.Email2, item.Email3 };
            int slot = 0;
            foreach (JsonElement email in emails.EnumerateArray()) {
                string? address = GetString(email, "address");
                if (string.IsNullOrWhiteSpace(address)) continue;
                if (slot == slots.Length) break;
                slots[slot].Address = address;
                slots[slot].AddressType = "SMTP";
                slots[slot].DisplayName = GetString(email, "name") ?? address;
                slot++;
            }
        }

        IReadOnlyList<string> businessPhones = GetStrings(contact, "businessPhones");
        IReadOnlyList<string> homePhones = GetStrings(contact, "homePhones");
        item.Phones.Business = Element(businessPhones, 0);
        item.Phones.Business2 = Element(businessPhones, 1);
        item.Phones.Home = Element(homePhones, 0);
        item.Phones.Home2 = Element(homePhones, 1);
        item.Phones.Mobile = GetString(contact, "mobilePhone");

        MapAddress(contact, "businessAddress", item.BusinessAddress);
        MapAddress(contact, "homeAddress", item.HomeAddress);
        MapAddress(contact, "otherAddress", item.OtherAddress);

        var document = new EmailDocument {
            OutlookItemKind = OutlookItemKind.Contact,
            MessageClass = "IPM.Contact",
            Subject = item.DisplayName ?? item.FileAs,
            Contact = item
        };
        document.Body.Text = GetString(contact, "personalNotes");
        ApplyCommon(contact, document);
        return document;
    }

    internal static EmailDocument MapEvent(JsonElement graphEvent, out string? warning) {
        warning = null;
        bool isAllDay = GetBoolean(graphEvent, "isAllDay") == true;
        DateTimeOffset? start = GetDateTimeTimeZone(graphEvent, "start");
        DateTimeOffset? end = GetDateTimeTimeZone(graphEvent, "end");
        var item = new OutlookAppointment {
            Start = start,
            End = end,
            IsAllDay = isAllDay,
            Location = GetLocation(graphEvent),
            BusyStatus = MapBusyStatus(GetString(graphEvent, "showAs")),
            MeetingStatus = GetBoolean(graphEvent, "isOrganizer") == false ? 3 : (HasAttendees(graphEvent) ? 1 : 0),
            ResponseStatus = MapResponseStatus(graphEvent)
        };
        if (GetBoolean(graphEvent, "isReminderOn") == true) {
            item.ReminderIsSet = true;
            item.ReminderDeltaMinutes = (int?)GetInt64(graphEvent, "reminderMinutesBeforeStart");
        } else if (GetBoolean(graphEvent, "isReminderOn") == false) {
            item.ReminderIsSet = false;
        }
        item.TimeZoneDescription = GetString(graphEvent, "originalStartTimeZone");

        if (graphEvent.TryGetProperty("recurrence", out JsonElement recurrence) && recurrence.ValueKind == JsonValueKind.Object) {
            item.Recurrence = MapRecurrence(recurrence, graphEvent, start, end, out warning);
            item.IsRecurring = item.Recurrence != null;
        }

        var document = new EmailDocument {
            OutlookItemKind = OutlookItemKind.Appointment,
            MessageClass = "IPM.Appointment",
            Subject = GetString(graphEvent, "subject"),
            Appointment = item
        };
        ApplyBody(graphEvent, document);
        ApplyCommon(graphEvent, document);
        EmailAddress? organizer = GetEmailAddress(graphEvent, "organizer");
        if (organizer != null) document.From = organizer;
        if (graphEvent.TryGetProperty("attendees", out JsonElement attendees) && attendees.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement attendee in attendees.EnumerateArray()) {
                EmailAddress? address = GetEmailAddress(attendee, null);
                if (address == null) continue;
                EmailRecipientKind kind = GetString(attendee, "type") switch {
                    "optional" => EmailRecipientKind.Cc,
                    "resource" => EmailRecipientKind.Resource,
                    _ => EmailRecipientKind.To
                };
                document.Recipients.Add(new EmailRecipient(kind, address));
            }
        }
        return document;
    }

    internal static EmailDocument MapTask(JsonElement task, out string? warning) {
        warning = null;
        string? status = GetString(task, "status");
        var item = new OutlookTask {
            Start = GetDateTimeTimeZone(task, "startDateTime"),
            Due = GetDateTimeTimeZone(task, "dueDateTime"),
            Status = status switch {
                "inProgress" => 1,
                "completed" => 2,
                "waitingOnOthers" => 3,
                "deferred" => 4,
                _ => 0
            }
        };
        item.IsComplete = item.Status == 2;
        item.PercentComplete = item.Status == 2 ? 1.0 : 0.0;
        if (GetBoolean(task, "isReminderOn") == true) {
            item.ReminderIsSet = true;
            item.ReminderTime = GetDateTimeTimeZone(task, "reminderDateTime");
        }
        if (task.TryGetProperty("recurrence", out JsonElement recurrence) && recurrence.ValueKind == JsonValueKind.Object) {
            warning = "The task recurrence pattern was not carried; the task is written as a single task.";
        }

        var document = new EmailDocument {
            OutlookItemKind = OutlookItemKind.Task,
            MessageClass = "IPM.Task",
            Subject = GetString(task, "title"),
            Task = item
        };
        ApplyBody(task, document);
        ApplyCommon(task, document);
        DateTimeOffset? completed = GetDateTimeTimeZone(task, "completedDateTime");
        if (completed.HasValue) document.MessageMetadata.ModifiedDate = completed;
        return document;
    }

    internal static DateTimeOffset? GetEventStart(JsonElement graphEvent) => GetDateTimeTimeZone(graphEvent, "start");

    internal static DateTimeOffset? GetEventEnd(JsonElement graphEvent) {
        if (graphEvent.TryGetProperty("recurrence", out JsonElement recurrence) && recurrence.ValueKind == JsonValueKind.Object &&
            recurrence.TryGetProperty("range", out JsonElement range)) {
            string? type = GetString(range, "type");
            if (type == "noEnd" || type == "numbered") return null;
            string? endDate = GetString(range, "endDate");
            if (endDate != null && DateTime.TryParse(endDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)) {
                return new DateTimeOffset(date.Date.AddDays(1), TimeSpan.Zero);
            }
        }
        return GetDateTimeTimeZone(graphEvent, "end");
    }

    private static OutlookRecurrence? MapRecurrence(JsonElement recurrence, JsonElement graphEvent,
        DateTimeOffset? start, DateTimeOffset? end, out string? warning) {
        warning = null;
        if (!start.HasValue || !recurrence.TryGetProperty("pattern", out JsonElement pattern) ||
            !recurrence.TryGetProperty("range", out JsonElement range)) {
            warning = "The recurrence pattern is incomplete; the series is written as its first occurrence.";
            return null;
        }
        TimeZoneInfo zone = ResolveTimeZone(GetString(range, "recurrenceTimeZone") ?? GetString(graphEvent, "originalStartTimeZone"));
        DateTime localStart = TimeZoneInfo.ConvertTime(start.Value, zone).DateTime;
        var result = new OutlookRecurrence {
            Start = localStart,
            Duration = end.HasValue && end.Value > start.Value ? end.Value - start.Value : TimeSpan.Zero,
            Interval = (int)Math.Max(1, GetInt64(pattern, "interval") ?? 1),
            TimeZoneId = zone.Id
        };
        string? firstDay = GetString(pattern, "firstDayOfWeek");
        if (firstDay != null && TryParseDay(firstDay, out DayOfWeek first)) result.FirstDayOfWeek = first;
        OutlookRecurrenceDays days = OutlookRecurrenceDays.None;
        foreach (string day in GetStrings(pattern, "daysOfWeek")) {
            if (TryParseDay(day, out DayOfWeek parsed)) days |= ToRecurrenceDay(parsed);
        }
        int? dayOfMonth = (int?)GetInt64(pattern, "dayOfMonth");
        OutlookRecurrenceWeekOrdinal ordinal = GetString(pattern, "index") switch {
            "second" => OutlookRecurrenceWeekOrdinal.Second,
            "third" => OutlookRecurrenceWeekOrdinal.Third,
            "fourth" => OutlookRecurrenceWeekOrdinal.Fourth,
            "last" => OutlookRecurrenceWeekOrdinal.Last,
            _ => OutlookRecurrenceWeekOrdinal.First
        };
        switch (GetString(pattern, "type")) {
            case "daily":
                result.Frequency = OutlookRecurrenceFrequency.Daily;
                result.PatternKind = OutlookRecurrencePatternKind.Day;
                break;
            case "weekly":
                result.Frequency = OutlookRecurrenceFrequency.Weekly;
                result.PatternKind = OutlookRecurrencePatternKind.Week;
                result.DaysOfWeek = days == OutlookRecurrenceDays.None ? ToRecurrenceDay(localStart.DayOfWeek) : days;
                break;
            case "absoluteMonthly":
                result.Frequency = OutlookRecurrenceFrequency.Monthly;
                result.PatternKind = OutlookRecurrencePatternKind.MonthDay;
                result.DayOfMonth = dayOfMonth ?? localStart.Day;
                break;
            case "relativeMonthly":
                result.Frequency = OutlookRecurrenceFrequency.Monthly;
                result.PatternKind = OutlookRecurrencePatternKind.MonthNth;
                result.DaysOfWeek = days;
                result.WeekOrdinal = ordinal;
                break;
            case "absoluteYearly":
                result.Frequency = OutlookRecurrenceFrequency.Yearly;
                result.PatternKind = OutlookRecurrencePatternKind.MonthDay;
                result.DayOfMonth = dayOfMonth ?? localStart.Day;
                break;
            case "relativeYearly":
                result.Frequency = OutlookRecurrenceFrequency.Yearly;
                result.PatternKind = OutlookRecurrencePatternKind.MonthNth;
                result.DaysOfWeek = days;
                result.WeekOrdinal = ordinal;
                break;
            default:
                warning = "The recurrence pattern type is not supported; the series is written as its first occurrence.";
                return null;
        }
        switch (GetString(range, "type")) {
            case "endDate":
                string? endDate = GetString(range, "endDate");
                if (endDate != null && DateTime.TryParse(endDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime until)) {
                    result.RangeKind = OutlookRecurrenceRangeKind.EndDate;
                    result.EndDate = until.Date;
                } else {
                    result.RangeKind = OutlookRecurrenceRangeKind.NoEnd;
                }
                break;
            case "numbered":
                result.RangeKind = OutlookRecurrenceRangeKind.OccurrenceCount;
                result.OccurrenceCount = (int)Math.Max(1, GetInt64(range, "numberOfOccurrences") ?? 1);
                break;
            default:
                result.RangeKind = OutlookRecurrenceRangeKind.NoEnd;
                break;
        }
        warning = "Modified and cancelled occurrences of the series are not carried.";
        return result;
    }

    private static OutlookRecurrenceDays ToRecurrenceDay(DayOfWeek day) => day switch {
        DayOfWeek.Sunday => OutlookRecurrenceDays.Sunday,
        DayOfWeek.Monday => OutlookRecurrenceDays.Monday,
        DayOfWeek.Tuesday => OutlookRecurrenceDays.Tuesday,
        DayOfWeek.Wednesday => OutlookRecurrenceDays.Wednesday,
        DayOfWeek.Thursday => OutlookRecurrenceDays.Thursday,
        DayOfWeek.Friday => OutlookRecurrenceDays.Friday,
        _ => OutlookRecurrenceDays.Saturday
    };

    private static bool TryParseDay(string value, out DayOfWeek day) =>
        Enum.TryParse(value, ignoreCase: true, out day);

    private static TimeZoneInfo ResolveTimeZone(string? id) {
        if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "tzone://Microsoft/Utc", StringComparison.OrdinalIgnoreCase)) {
            return TimeZoneInfo.Utc;
        }
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(id!);
        } catch (TimeZoneNotFoundException) {
            return TimeZoneInfo.Utc;
        } catch (InvalidTimeZoneException) {
            return TimeZoneInfo.Utc;
        }
    }

    private static int? MapBusyStatus(string? showAs) => showAs switch {
        "free" => 0,
        "tentative" => 1,
        "busy" => 2,
        "oof" => 3,
        "workingElsewhere" => 4,
        _ => null
    };

    private static int? MapResponseStatus(JsonElement graphEvent) {
        if (!graphEvent.TryGetProperty("responseStatus", out JsonElement status)) return null;
        return GetString(status, "response") switch {
            "organizer" => 1,
            "tentativelyAccepted" => 2,
            "accepted" => 3,
            "declined" => 4,
            "notResponded" => 5,
            "none" => 0,
            _ => null
        };
    }

    private static bool HasAttendees(JsonElement graphEvent) =>
        graphEvent.TryGetProperty("attendees", out JsonElement attendees) &&
        attendees.ValueKind == JsonValueKind.Array &&
        attendees.GetArrayLength() > 0;

    private static string? GetLocation(JsonElement graphEvent) =>
        graphEvent.TryGetProperty("location", out JsonElement location)
            ? GetString(location, "displayName") is { Length: > 0 } name ? name : null
            : null;

    private static void ApplyBody(JsonElement resource, EmailDocument document) {
        if (!resource.TryGetProperty("body", out JsonElement body)) return;
        string? content = GetString(body, "content");
        if (string.IsNullOrEmpty(content)) return;
        if (string.Equals(GetString(body, "contentType"), "html", StringComparison.OrdinalIgnoreCase)) {
            document.Body.Html = content;
        } else {
            document.Body.Text = content;
        }
    }

    private static void ApplyCommon(JsonElement resource, EmailDocument document) {
        foreach (string category in GetStrings(resource, "categories")) document.MessageMetadata.Categories.AddIfMissing(category);
        DateTimeOffset? created = GetDateTimeOffset(resource, "createdDateTime");
        DateTimeOffset? modified = GetDateTimeOffset(resource, "lastModifiedDateTime");
        if (created.HasValue) document.MessageMetadata.CreatedDate = created;
        if (modified.HasValue) document.MessageMetadata.ModifiedDate = modified;
        document.MessageMetadata.Importance = GetString(resource, "importance") switch {
            "low" => EmailMessageImportance.Low,
            "high" => EmailMessageImportance.High,
            "normal" => EmailMessageImportance.Normal,
            _ => document.MessageMetadata.Importance
        };
        document.Date = created ?? modified;
    }

    private static EmailAddress? GetEmailAddress(JsonElement owner, string? propertyName) {
        JsonElement container = owner;
        if (propertyName != null && !owner.TryGetProperty(propertyName, out container)) return null;
        if (!container.TryGetProperty("emailAddress", out JsonElement email)) return null;
        string? address = GetString(email, "address");
        string? name = GetString(email, "name");
        return string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(name) ? null : new EmailAddress(address, name);
    }

    private static void MapAddress(JsonElement contact, string name, OutlookPostalAddress target) {
        if (!contact.TryGetProperty(name, out JsonElement address) || address.ValueKind != JsonValueKind.Object) return;
        target.Street = GetString(address, "street");
        target.City = GetString(address, "city");
        target.StateOrProvince = GetString(address, "state");
        target.PostalCode = GetString(address, "postalCode");
        target.Country = GetString(address, "countryOrRegion");
    }

    /// <summary>Reads a Graph dateTimeTimeZone value. Values in other zones are converted through the system time zone database.</summary>
    private static DateTimeOffset? GetDateTimeTimeZone(JsonElement resource, string name) {
        if (!resource.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object) return null;
        string? text = GetString(value, "dateTime");
        if (text == null || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local)) {
            return null;
        }
        TimeZoneInfo zone = ResolveTimeZone(GetString(value, "timeZone"));
        DateTime unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        TimeSpan offset = zone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    private static string? Element(IReadOnlyList<string> values, int index) => index < values.Count ? values[index] : null;
}
