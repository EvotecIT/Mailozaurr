using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests;

public class ComposeProfileUtilitiesTests {
    [Fact]
    public void NormalizeProfiles_AppliesFallbackValues_AndKeepsOneDefault() {
        var profiles = new[] {
            new MailComposeProfile {
                Id = "sales",
                Name = "Sales",
                From = "sales@example.com",
                IsDefault = true
            },
            new MailComposeProfile {
                Name = "Billing",
                From = "billing@example.com",
                ReplyTo = "billing-replies@example.com"
            }
        };

        var fallback = new MailComposeProfile {
            ReplyTo = "reply@example.com",
            SignatureText = "Regards",
            IsDefault = true
        };

        var normalized = ComposeProfileUtilities.NormalizeProfiles(profiles, fallback);

        Assert.Equal(2, normalized.Count);
        Assert.Equal("sales", normalized[0].Id);
        Assert.Equal("reply@example.com", normalized[0].ReplyTo);
        Assert.Equal("Regards", normalized[0].SignatureText);
        Assert.True(normalized[0].IsDefault);
        Assert.Equal("billing", normalized[1].Id);
        Assert.Equal("billing-replies@example.com", normalized[1].ReplyTo);
        Assert.False(normalized[1].IsDefault);
    }

    [Fact]
    public void NormalizeProfiles_SynthesizesFallbackProfile_WhenConfiguredProfilesMissing() {
        var normalized = ComposeProfileUtilities.NormalizeProfiles(
            profiles: null,
            fallbackProfile: new MailComposeProfile {
                From = "default@example.com",
                ReplyTo = "reply@example.com",
                SignatureText = "Thanks"
            });

        Assert.Single(normalized);
        Assert.Equal("default", normalized[0].Id);
        Assert.Equal("default@example.com", normalized[0].From);
        Assert.True(normalized[0].IsDefault);
    }

    [Fact]
    public void GetDefaultProfile_ReturnsFirstMarkedDefault() {
        var profiles = new List<MailComposeProfile> {
            new MailComposeProfile { Id = "one", IsDefault = false },
            new MailComposeProfile { Id = "two", IsDefault = true }
        };

        var selected = ComposeProfileUtilities.GetDefaultProfile(profiles);

        Assert.NotNull(selected);
        Assert.Equal("two", selected!.Id);
    }
}