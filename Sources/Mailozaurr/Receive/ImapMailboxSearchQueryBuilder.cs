using System;
using System.Collections.Generic;
using MailKit.Search;

namespace Mailozaurr;

/// <summary>
/// Builds IMAP <see cref="SearchQuery"/> instances from common mailbox search filters.
/// </summary>
public static class ImapMailboxSearchQueryBuilder {
    /// <summary>
    /// Builds IMAP search query with optional field filters and free-text tokens.
    /// </summary>
    public static SearchQuery Build(
        bool unseenOnly = false,
        string? subjectContains = null,
        string? fromContains = null,
        string? toContains = null,
        string? bodyContains = null,
        DateTime? sinceUtc = null,
        DateTime? beforeUtc = null,
        string? query = null) {
        var search = SearchQuery.All;
        if (unseenOnly) {
            search = search.And(SearchQuery.NotSeen);
        }

        var subject = NormalizeOptional(subjectContains);
        if (subject != null) {
            search = search.And(SearchQuery.SubjectContains(subject));
        }

        var from = NormalizeOptional(fromContains);
        if (from != null) {
            search = search.And(SearchQuery.FromContains(from));
        }

        var to = NormalizeOptional(toContains);
        if (to != null) {
            search = search.And(SearchQuery.ToContains(to));
        }

        var body = NormalizeOptional(bodyContains);
        if (body != null) {
            search = search.And(SearchQuery.BodyContains(body));
        }

        if (sinceUtc.HasValue) {
            search = search.And(SearchQuery.SentSince(sinceUtc.Value));
        }
        if (beforeUtc.HasValue) {
            search = search.And(SearchQuery.SentBefore(beforeUtc.Value));
        }

        var tokens = Tokenize(query);
        foreach (var token in tokens) {
            // Pragmatic OR across common fields, AND'ed across tokens.
            var tokenQuery = SearchQuery.SubjectContains(token)
                .Or(SearchQuery.FromContains(token))
                .Or(SearchQuery.ToContains(token))
                .Or(SearchQuery.BodyContains(token));
            search = search.And(tokenQuery);
        }

        return search;
    }

    /// <summary>
    /// Splits free-text query into non-empty whitespace-delimited terms.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string? query) {
        var normalized = NormalizeOptional(query);
        if (normalized == null) {
            return Array.Empty<string>();
        }

        return normalized.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static string? NormalizeOptional(string? value) {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
