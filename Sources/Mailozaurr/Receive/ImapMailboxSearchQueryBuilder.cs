#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using MailKit.Search;

namespace Mailozaurr;

public static class ImapMailboxSearchQueryBuilder {
    public static SearchQuery Build(
        bool unseenOnly,
        string? subjectContains,
        string? fromContains,
        string? toContains,
        string? bodyContains,
        DateTime? sinceUtc,
        DateTime? beforeUtc,
        string? query) {
        SearchQuery? combined = null;

        static void Add(ref SearchQuery? current, SearchQuery part) {
            current = current is null ? part : current.And(part);
        }

        if (unseenOnly) {
            Add(ref combined, SearchQuery.NotSeen);
        }
        if (!string.IsNullOrWhiteSpace(subjectContains)) {
            Add(ref combined, SearchQuery.SubjectContains(subjectContains.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(fromContains)) {
            Add(ref combined, SearchQuery.FromContains(fromContains.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(toContains)) {
            Add(ref combined, SearchQuery.ToContains(toContains.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(bodyContains)) {
            Add(ref combined, SearchQuery.BodyContains(bodyContains.Trim()));
        }
        if (sinceUtc.HasValue) {
            Add(ref combined, SearchQuery.DeliveredAfter(sinceUtc.Value.ToUniversalTime()));
        }
        if (beforeUtc.HasValue) {
            Add(ref combined, SearchQuery.DeliveredBefore(beforeUtc.Value.ToUniversalTime()));
        }
        if (!string.IsNullOrWhiteSpace(query)) {
            var q = query.Trim();
            var freeText = SearchQuery.SubjectContains(q)
                .Or(SearchQuery.BodyContains(q))
                .Or(SearchQuery.FromContains(q))
                .Or(SearchQuery.ToContains(q));
            Add(ref combined, freeText);
        }

        return combined ?? SearchQuery.All;
    }
}
