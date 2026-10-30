using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileStoreTests {
    [Fact]
    public async Task FileProfileStoreRoundTripsSmtpDeliveryPolicyWithIndependentSnapshots() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);
        var profile = new MailProfile {
            Id = "smtp-policy", DisplayName = "SMTP policy", Kind = MailProfileKind.Smtp,
            SmtpDelivery = new SmtpDeliveryOptions {
                Strategy = SmtpServerStrategy.RoundRobin, MaxAttempts = 7, CircuitBreakMinutes = 5,
                Servers = new List<SmtpEndpoint> {
                    new() { Server = "primary.example.invalid", Port = 587, SecureSocketOptions = MailKit.Security.SecureSocketOptions.StartTls },
                    new() { Server = "secondary.example.invalid", Port = 465, SecureSocketOptions = MailKit.Security.SecureSocketOptions.SslOnConnect },
                    new() { Server = "inherited.example.invalid", Port = 2525 }
                }
            }
        };
        await store.SaveAsync(profile);
        var loaded = await new FileMailProfileStore(filePath).GetByIdAsync(profile.Id);
        Assert.NotNull(loaded);
        var policy = Assert.IsType<SmtpDeliveryOptions>(loaded.SmtpDelivery);
        Assert.Equal(SmtpServerStrategy.RoundRobin, policy.Strategy);
        Assert.Equal(7, policy.MaxAttempts);
        Assert.Equal(5, policy.CircuitBreakMinutes);
        Assert.Collection(policy.Servers,
            endpoint => {
                Assert.Equal("primary.example.invalid", endpoint.Server);
                Assert.Equal(587, endpoint.Port);
                Assert.Equal(MailKit.Security.SecureSocketOptions.StartTls, endpoint.SecureSocketOptions);
            },
            endpoint => {
                Assert.Equal("secondary.example.invalid", endpoint.Server);
                Assert.Equal(465, endpoint.Port);
                Assert.Equal(MailKit.Security.SecureSocketOptions.SslOnConnect, endpoint.SecureSocketOptions);
            },
            endpoint => {
                Assert.Equal("inherited.example.invalid", endpoint.Server);
                Assert.Equal(2525, endpoint.Port);
                Assert.Null(endpoint.SecureSocketOptions);
            });
        var independent = Assert.Single(await store.GetAllAsync());
        policy.Servers[0].Server = "edited.example.invalid";
        policy.Servers[0].Port = 25;
        policy.Servers[0].SecureSocketOptions = MailKit.Security.SecureSocketOptions.None;
        policy.Servers.RemoveAt(1);
        policy.Strategy = SmtpServerStrategy.Random;
        policy.MaxAttempts = 1;
        policy.CircuitBreakMinutes = 0;
        var persisted = await new FileMailProfileStore(filePath).GetByIdAsync(profile.Id);
        Assert.NotNull(persisted);
        foreach (var snapshot in new[] { profile, independent, persisted }) {
            var unchanged = Assert.IsType<SmtpDeliveryOptions>(snapshot.SmtpDelivery);
            Assert.Equal(3, unchanged.Servers.Count);
            Assert.Equal("primary.example.invalid", unchanged.Servers[0].Server);
            Assert.Equal(587, unchanged.Servers[0].Port);
            Assert.Equal(MailKit.Security.SecureSocketOptions.StartTls, unchanged.Servers[0].SecureSocketOptions);
            Assert.Equal("secondary.example.invalid", unchanged.Servers[1].Server);
            Assert.Equal(SmtpServerStrategy.RoundRobin, unchanged.Strategy);
            Assert.Equal(7, unchanged.MaxAttempts);
            Assert.Equal(5, unchanged.CircuitBreakMinutes);
        }
    }

    [Fact]
    public async Task FileProfileStoreRoundTripsProfiles() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);

        var profile = new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["server"] = "imap.example.com"
            }
        };

        await store.SaveAsync(profile);
        var loaded = await store.GetByIdAsync(profile.Id);
        var all = await store.GetAllAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Work IMAP", loaded!.DisplayName);
        Assert.Equal("imap.example.com", loaded.Settings["server"]);
        Assert.Single(all);
    }

    [Fact]
    public async Task SavingDefaultProfileClearsPreviousDefault() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);

        await store.SaveAsync(new MailProfile {
            Id = "one",
            DisplayName = "One",
            Kind = MailProfileKind.Imap,
            IsDefault = true
        });
        await store.SaveAsync(new MailProfile {
            Id = "two",
            DisplayName = "Two",
            Kind = MailProfileKind.Graph,
            IsDefault = true
        });

        var all = await store.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.False(all.Single(p => p.Id == "one").IsDefault);
        Assert.True(all.Single(p => p.Id == "two").IsDefault);
    }

    [Fact]
    public async Task RemoveReturnsFalseWhenProfileDoesNotExist() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);

        var removed = await store.RemoveAsync("missing");

        Assert.False(removed);
    }

    [Fact]
    public async Task UpdatingProfileReplacesFileWithoutLeavingTemporaryArtifacts() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);

        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Initial",
            Kind = MailProfileKind.Imap
        });
        await store.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Updated",
            Kind = MailProfileKind.Imap
        });

        var loaded = await store.GetByIdAsync("work-imap");
        var files = Directory.GetFiles(Path.GetDirectoryName(filePath)!);

        Assert.NotNull(loaded);
        Assert.Equal("Updated", loaded!.DisplayName);
        Assert.Single(files);
        Assert.Equal(filePath, files[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeparateStoreInstancesDoNotLoseConcurrentProfileWrites() {
        var filePath = CreateTemporaryFilePath();

        await Task.WhenAll(Enumerable.Range(0, 24).Select(index =>
            new FileMailProfileStore(filePath).SaveAsync(new MailProfile {
                Id = $"profile-{index}",
                DisplayName = $"Profile {index}",
                Kind = MailProfileKind.Imap
            })));

        var profiles = await new FileMailProfileStore(filePath).GetAllAsync();
        Assert.Equal(24, profiles.Count);
        Assert.Equal(24, profiles.Select(profile => profile.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task ReadingExistingProfileFileDoesNotCreateLockArtifacts() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);
        await store.SaveAsync(new MailProfile {
            Id = "inspection-only",
            DisplayName = "Inspection only",
            Kind = MailProfileKind.Imap
        });
        string lockDirectory = Path.Combine(Path.GetDirectoryName(filePath)!, ".mailozaurr-locks");
        Directory.Delete(lockDirectory, true);
        FileAttributes originalAttributes = File.GetAttributes(filePath);
        File.SetAttributes(filePath, originalAttributes | FileAttributes.ReadOnly);

        try {
            var profiles = await new FileMailProfileStore(filePath).GetAllAsync();

            Assert.Single(profiles);
            Assert.Equal("inspection-only", profiles[0].Id);
            Assert.False(Directory.Exists(lockDirectory));
        } finally {
            File.SetAttributes(filePath, originalAttributes);
        }
    }

    private static string CreateTemporaryFilePath() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "profiles.json");
    }
}
