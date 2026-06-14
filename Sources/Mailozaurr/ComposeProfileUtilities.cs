using System;
using System.Collections.Generic;
using System.Linq;

namespace Mailozaurr;

/// <summary>
/// Helper methods for normalizing reusable compose profiles.
/// </summary>
public static class ComposeProfileUtilities {
    /// <summary>
    /// Normalizes compose profiles and guarantees at most one default entry.
    /// </summary>
    /// <param name="profiles">Configured profiles.</param>
    /// <param name="fallbackProfile">Fallback values applied to profiles missing fields.</param>
    /// <returns>Normalized profile list.</returns>
    public static IReadOnlyList<MailComposeProfile> NormalizeProfiles(
        IEnumerable<MailComposeProfile>? profiles,
        MailComposeProfile? fallbackProfile = null) {
        var normalizedFallback = NormalizeSingle(fallbackProfile, "default", 0, fallbackProfile);
        var items = new List<MailComposeProfile>();
        var index = 0;

        if (profiles is not null) {
            foreach (var profile in profiles) {
                var normalized = NormalizeSingle(profile, profile?.Id, index, normalizedFallback);
                if (normalized is not null) {
                    items.Add(normalized);
                }
                index++;
            }
        }

        if (items.Count == 0 && HasMeaningfulContent(normalizedFallback)) {
            items.Add(new MailComposeProfile {
                Id = normalizedFallback!.Id,
                Name = normalizedFallback.Name,
                From = normalizedFallback.From,
                ReplyTo = normalizedFallback.ReplyTo,
                SignatureText = normalizedFallback.SignatureText,
                IsDefault = true
            });
        }

        if (items.Count == 0) {
            return items;
        }

        var selectedIndex = items.FindIndex(static profile => profile.IsDefault);
        if (selectedIndex < 0) {
            selectedIndex = 0;
        }

        for (var i = 0; i < items.Count; i++) {
            items[i].IsDefault = i == selectedIndex;
        }

        return items;
    }

    /// <summary>
    /// Resolves the default profile from a list of profiles.
    /// </summary>
    /// <param name="profiles">Profiles to inspect.</param>
    /// <returns>The default profile, when available.</returns>
    public static MailComposeProfile? GetDefaultProfile(IEnumerable<MailComposeProfile>? profiles) {
        if (profiles is null) {
            return null;
        }

        MailComposeProfile? first = null;
        foreach (var profile in profiles) {
            first ??= profile;
            if (profile?.IsDefault == true) {
                return profile;
            }
        }

        return first;
    }

    private static MailComposeProfile? NormalizeSingle(
        MailComposeProfile? profile,
        string? rawId,
        int index,
        MailComposeProfile? fallbackProfile) {
        var normalizedId = NormalizeOptional(profile?.Id) ?? NormalizeOptional(rawId);
        var from = NormalizeOptional(profile?.From) ?? NormalizeOptional(fallbackProfile?.From);
        var replyTo = NormalizeOptional(profile?.ReplyTo) ?? NormalizeOptional(fallbackProfile?.ReplyTo);
        var signature = NormalizeOptional(profile?.SignatureText) ?? NormalizeOptional(fallbackProfile?.SignatureText);
        var name = NormalizeOptional(profile?.Name);

        if (string.IsNullOrWhiteSpace(name) &&
            string.IsNullOrWhiteSpace(from) &&
            string.IsNullOrWhiteSpace(replyTo) &&
            string.IsNullOrWhiteSpace(signature)) {
            return null;
        }

        normalizedId = BuildProfileId(normalizedId, name, from, index);
        name ??= from ?? normalizedId;

        return new MailComposeProfile {
            Id = normalizedId,
            Name = name,
            From = from,
            ReplyTo = replyTo,
            SignatureText = signature,
            IsDefault = profile?.IsDefault ?? fallbackProfile?.IsDefault ?? false
        };
    }

    private static bool HasMeaningfulContent(MailComposeProfile? profile) =>
        !string.IsNullOrWhiteSpace(profile?.Name) ||
        !string.IsNullOrWhiteSpace(profile?.From) ||
        !string.IsNullOrWhiteSpace(profile?.ReplyTo) ||
        !string.IsNullOrWhiteSpace(profile?.SignatureText);

    private static string BuildProfileId(string? rawId, string? name, string? from, int index) {
        if (rawId is not null) {
            var trimmedId = rawId.Trim();
            if (trimmedId.Length > 0) {
                return trimmedId;
            }
        }

        string? source = null;
        if (name is not null) {
            var trimmedName = name.Trim();
            if (trimmedName.Length > 0) {
                source = trimmedName;
            }
        }
        if (source is null && from is not null) {
            var trimmedFrom = from.Trim();
            if (trimmedFrom.Length > 0) {
                source = trimmedFrom;
            }
        }

        if (source is not null) {
            var normalizedSource = source.Trim();
            var chars = normalizedSource
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            var collapsed = new string(chars).Trim('-');
            if (!string.IsNullOrWhiteSpace(collapsed)) {
                return collapsed;
            }
        }

        return $"profile-{index + 1}";
    }

    private static string? NormalizeOptional(string? value) {
        if (value is null) {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0) {
            return null;
        }

        return trimmed;
    }
}