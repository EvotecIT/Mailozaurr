using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileServiceTests {
    [Fact]
    public async Task CreateAsyncAllowsOnlyOneConcurrentInMemoryCreator() {
        var service = new MailProfileService(new InMemoryMailProfileStore());

        OperationResult[] results = await Task.WhenAll(
            service.CreateAsync(CreateImapProfile("shared", "First")),
            service.CreateAsync(CreateImapProfile("shared", "Second")));

        Assert.Single(results, result => result.Succeeded);
        OperationResult rejected = Assert.Single(results, result => !result.Succeeded);
        Assert.Equal("profile_already_exists", rejected.Code);
        MailProfile stored = Assert.IsType<MailProfile>(await service.GetProfileAsync("shared"));
        Assert.Contains(stored.DisplayName, new[] { "First", "Second" });
    }

    [Fact]
    public async Task CreateAsyncAllowsOnlyOneConcurrentFileStoreCreator() {
        string path = CreateTemporaryFilePath("profiles.json");
        var firstService = new MailProfileService(new FileMailProfileStore(path));
        var secondService = new MailProfileService(new FileMailProfileStore(path));

        OperationResult[] results = await Task.WhenAll(
            firstService.CreateAsync(CreateImapProfile("shared", "First")),
            secondService.CreateAsync(CreateImapProfile("shared", "Second")));

        Assert.Single(results, result => result.Succeeded);
        OperationResult rejected = Assert.Single(results, result => !result.Succeeded);
        Assert.Equal("profile_already_exists", rejected.Code);
        MailProfile stored = Assert.IsType<MailProfile>(await firstService.GetProfileAsync("shared"));
        Assert.Contains(stored.DisplayName, new[] { "First", "Second" });
    }

    [Fact]
    public async Task SaveAsyncReturnsValidationErrorForInvalidProfile() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var service = new MailProfileService(store);

        var result = await service.SaveAsync(new MailProfile());

        Assert.False(result.Succeeded);
        Assert.Equal("profile_invalid", result.Code);
    }

    private static MailProfile CreateImapProfile(string id, string displayName) => new() {
        Id = id,
        DisplayName = displayName,
        Kind = MailProfileKind.Imap,
        Settings = new Dictionary<string, string> {
            [MailProfileSettingsKeys.Server] = "imap.example.com"
        }
    };

    [Fact]
    public async Task SetDefaultAsyncMarksRequestedProfileAsDefault() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var service = new MailProfileService(store);

        await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });
        await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph,
            DefaultMailbox = "user@example.com"
        });

        var result = await service.SetDefaultAsync("graph");
        var profiles = await service.GetProfilesAsync();

        Assert.True(result.Succeeded);
        Assert.False(profiles.Single(p => p.Id == "imap").IsDefault);
        Assert.True(profiles.Single(p => p.Id == "graph").IsDefault);
    }

    [Fact]
    public async Task DeleteAsyncRemovesKnownAndCustomSecretsWhenSecretStoreIsProvided() {
        var profilePath = CreateTemporaryFilePath("profiles.json");
        var secretPath = CreateTemporaryFilePath("secrets.json");
        var store = new FileMailProfileStore(profilePath);
        var secretStore = new FileMailSecretStore(secretPath, new TestCredentialProtector());
        var service = new MailProfileService(store, secretStore);

        await service.SaveAsync(new MailProfile {
            Id = "smtp",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "smtp.example.com" }
        });
        await secretStore.SetSecretAsync("smtp", MailSecretNames.Password, "secret");
        await secretStore.SetSecretAsync("smtp", "custom-api-key", "custom-secret");

        var result = await service.DeleteAsync("smtp");
        var secret = await secretStore.GetSecretAsync("smtp", MailSecretNames.Password);
        var customSecret = await secretStore.GetSecretAsync("smtp", "custom-api-key");

        Assert.True(result.Succeeded);
        Assert.Null(secret);
        Assert.Null(customSecret);
    }

    [Fact]
    public async Task DeleteAsyncRestoresProfileWhenSecretCleanupFails() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new FailingCleanupSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "smtp",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await secretStore.SetSecretAsync("smtp", "custom-api-key", "custom-secret");

        await Assert.ThrowsAsync<IOException>(() => service.DeleteAsync("smtp"));

        Assert.NotNull(await service.GetProfileAsync("smtp"));
        Assert.Equal("custom-secret", await secretStore.GetSecretAsync("smtp", "custom-api-key"));
    }

    [Fact]
    public async Task DeleteAsyncDoesNotDecryptSecretsBeforeRemovingProfile() {
        var profilePath = CreateTemporaryFilePath("profiles.json");
        var secretPath = CreateTemporaryFilePath("secrets.json");
        var profileStore = new FileMailProfileStore(profilePath);
        var secretStore = new FileMailSecretStore(secretPath, new UnreadableCredentialProtector());
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "smtp",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await secretStore.SetSecretAsync("smtp", "broken-secret", "cannot-be-unprotected");

        OperationResult result = await service.DeleteAsync("smtp");

        Assert.True(result.Succeeded);
        Assert.Null(await service.GetProfileAsync("smtp"));
        Assert.Null(await secretStore.GetSecretAsync("smtp", "broken-secret"));
    }

    [Fact]
    public async Task DeleteAsyncPurgesLegacySecretsUsingKnownProfileIdentity() {
        var profilePath = CreateTemporaryFilePath("profiles.json");
        var secretPath = CreateTemporaryFilePath("secrets.json");
        var profileStore = new FileMailProfileStore(profilePath);
        var protector = new TestCredentialProtector();
        string archiveValue = protector.Protect("archive-secret");
        File.WriteAllText(secretPath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"team::archive::password\":\"{archiveValue}\"}}}}");
        var secretStore = new FileMailSecretStore(secretPath, protector);
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "team",
            DisplayName = "Team",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await service.SaveAsync(new MailProfile {
            Id = "team::archive",
            DisplayName = "Team archive",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        OperationResult result = await service.DeleteAsync("team::archive");

        Assert.True(result.Succeeded);
        Assert.Null(await secretStore.GetSecretAsync("team::archive", "password"));
        Assert.NotNull(await service.GetProfileAsync("team"));
    }

    [Theory]
    [InlineData(MailSecretNames.ApiKey)]
    [InlineData(MailSecretNames.AccessKeyId)]
    [InlineData(MailSecretNames.SecretAccessKey)]
    public async Task DeleteAsyncRemovesProviderSecretsFromBasicStores(string secretName) {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Provider",
            Kind = MailProfileKind.SendGrid
        });
        await secretStore.SetSecretAsync("provider", secretName, "secret");

        var result = await service.DeleteAsync("provider");

        Assert.True(result.Succeeded);
        Assert.Null(await secretStore.GetSecretAsync("provider", secretName));
    }

    [Fact]
    public async Task SaveAsyncRejectsProviderKindChangesWithoutReusingSecrets() {
        var profileStore = new InMemoryMailProfileStore();
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await secretStore.SetSecretAsync("provider", MailSecretNames.Password, "smtp-secret");

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "SendGrid",
            Kind = MailProfileKind.SendGrid
        });

        Assert.False(result.Succeeded);
        Assert.Equal("profile_kind_change_not_allowed", result.Code);
        Assert.Equal(MailProfileKind.Smtp, (await service.GetProfileAsync("provider"))!.Kind);
        Assert.Equal("smtp-secret", await secretStore.GetSecretAsync("provider", MailSecretNames.Password));
    }

    [Theory]
    [InlineData(MailProfileKind.Smtp, MailProfileSettingsKeys.Server, "smtp.example.com", "smtp.attacker.example")]
    [InlineData(MailProfileKind.Imap, MailProfileSettingsKeys.Port, "993", "143")]
    [InlineData(MailProfileKind.Pop3, MailProfileSettingsKeys.SecureSocketOptions, "SslOnConnect", "None")]
    [InlineData(MailProfileKind.Smtp, MailProfileSettingsKeys.UserName, "sender@example.com", "other@example.com")]
    [InlineData(MailProfileKind.Imap, MailProfileSettingsKeys.UserName, "reader@example.com", "other@example.com")]
    [InlineData(MailProfileKind.Pop3, MailProfileSettingsKeys.UserName, "reader@example.com", "other@example.com")]
    [InlineData(MailProfileKind.Smtp, MailProfileSettingsKeys.UseSsl, "true", "false")]
    [InlineData(MailProfileKind.Pop3, MailProfileSettingsKeys.SkipCertificateRevocation, "false", "true")]
    [InlineData(MailProfileKind.Imap, MailProfileSettingsKeys.SkipCertificateValidation, "false", "true")]
    [InlineData(MailProfileKind.Graph, MailProfileSettingsKeys.Mailbox, "user@example.com", "other@example.com")]
    [InlineData(MailProfileKind.Graph, MailProfileSettingsKeys.ClientId, "graph-client", "other-client")]
    [InlineData(MailProfileKind.Graph, MailProfileSettingsKeys.TenantId, "tenant-a", "tenant-b")]
    [InlineData(MailProfileKind.Gmail, MailProfileSettingsKeys.Mailbox, "user@gmail.com", "other@gmail.com")]
    [InlineData(MailProfileKind.Gmail, MailProfileSettingsKeys.ClientId, "gmail-client", "other-client")]
    [InlineData(MailProfileKind.Jmap, MailProfileSettingsKeys.JmapSessionUrl, "https://mail.example.com/.well-known/jmap", "https://mail.attacker.example/.well-known/jmap")]
    [InlineData(MailProfileKind.Jmap, MailProfileSettingsKeys.JmapAccountId, "account-a", "account-b")]
    [InlineData(MailProfileKind.Jmap, MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl, "false", "true")]
    [InlineData(MailProfileKind.Ses, MailProfileSettingsKeys.Region, "us-east-1", "eu-central-1")]
    public async Task SaveAsyncRejectsCredentialContextChangesWithoutRedirectingSecrets(
        MailProfileKind kind,
        string setting,
        string originalValue,
        string changedValue) {
        var profileStore = new InMemoryMailProfileStore();
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(profileStore, secretStore);
        var originalSettings = CreateValidSettings(kind);
        originalSettings[setting] = originalValue;
        await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = kind.ToString(),
            Kind = kind,
            Settings = originalSettings
        });
        string secretName = kind is MailProfileKind.Graph or MailProfileKind.Gmail
            ? MailSecretNames.AccessToken
            : MailSecretNames.Password;
        await secretStore.SetSecretAsync("provider", secretName, "retained-secret");

        var changedSettings = CreateValidSettings(kind);
        changedSettings[setting] = changedValue;
        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Changed",
            Kind = kind,
            Settings = changedSettings
        });

        Assert.False(result.Succeeded);
        Assert.Equal("profile_credential_context_change_not_allowed", result.Code);
        Assert.Equal(originalValue, (await service.GetProfileAsync("provider"))!.Settings[setting]);
        Assert.Equal("retained-secret", await secretStore.GetSecretAsync("provider", secretName));
    }

    [Fact]
    public async Task SaveAsyncAllowsGraphContextBindingWhenNoSecretsExist() {
        var store = new InMemoryMailProfileStore();
        var secretStore = new InMemoryMailSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph,
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-a",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            }
        });

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph bound",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-b",
                [MailProfileSettingsKeys.TenantId] = "tenant-b"
            }
        });

        Assert.True(result.Succeeded);
        MailProfile saved = (await service.GetProfileAsync("graph"))!;
        Assert.Equal("client-b", saved.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("tenant-b", saved.Settings[MailProfileSettingsKeys.TenantId]);
    }

    [Fact]
    public async Task SaveAsyncAllowsFileStoredGraphContextBindingWhenNoSecretsExist() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new FileMailSecretStore(
            CreateTemporaryFilePath("secrets.json"),
            new TestCredentialProtector());
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-a",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            }
        });

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph rebound",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-b",
                [MailProfileSettingsKeys.TenantId] = "tenant-b"
            }
        });

        Assert.True(result.Succeeded);
        MailProfile saved = (await service.GetProfileAsync("graph"))!;
        Assert.Equal("client-b", saved.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("tenant-b", saved.Settings[MailProfileSettingsKeys.TenantId]);
    }

    [Fact]
    public async Task SaveAsyncRejectsGraphContextChangeWhenAnyCustomSecretExists() {
        var store = new InMemoryMailProfileStore();
        var secretStore = new InMemoryMailSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-a",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            }
        });
        await secretStore.SetSecretAsync("graph", "custom-token", "retained-secret");

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Redirected Graph",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-b",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("profile_credential_context_change_not_allowed", result.Code);
        Assert.Equal("client-a", (await service.GetProfileAsync("graph"))!.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("retained-secret", await secretStore.GetSecretAsync("graph", "custom-token"));
    }

    [Fact]
    public async Task SaveAsyncFailsClosedWhenSecretStoreCannotCoordinateContextBinding() {
        var store = new InMemoryMailProfileStore();
        var service = new MailProfileService(store, new BasicSecretStore());
        await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-a",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            }
        });

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph rebound",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-b",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("profile_credential_context_change_not_allowed", result.Code);
    }

    [Fact]
    public async Task SaveAsyncSerializesContextBindingWithConcurrentSecretWrites() {
        var store = new InMemoryMailProfileStore();
        var secretStore = new BlockingCoordinatedSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-a",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            }
        });

        Task<OperationResult> save = service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph rebound",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-b",
                [MailProfileSettingsKeys.TenantId] = "tenant-b"
            }
        });
        await secretStore.WaitForContextBindingAsync();
        Task concurrentSecretWrite = secretStore.SetSecretAsync("graph", "custom-token", "new-context-secret");

        Assert.False(concurrentSecretWrite.IsCompleted);
        secretStore.AllowContextBinding();
        Assert.True((await save).Succeeded);
        await concurrentSecretWrite;
        Assert.Equal("client-b", (await service.GetProfileAsync("graph"))!.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("new-context-secret", await secretStore.GetSecretAsync("graph", "custom-token"));
    }

    [Fact]
    public async Task SaveAsyncAllowsNonCredentialSettingsAndEquivalentEndpointFormatting() {
        var store = new InMemoryMailProfileStore();
        var service = new MailProfileService(store);
        await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "IMAP.Example.com",
                [MailProfileSettingsKeys.Port] = "0993",
                [MailProfileSettingsKeys.Folder] = "Inbox"
            }
        });

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "Updated IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com",
                [MailProfileSettingsKeys.Port] = "993",
                [MailProfileSettingsKeys.Folder] = "Archive"
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Archive", (await service.GetProfileAsync("imap"))!.Settings[MailProfileSettingsKeys.Folder]);
    }

    [Fact]
    public async Task SaveAsyncAllowsExplicitSesDefaultRegionWhenItWasPreviouslyOmitted() {
        var store = new InMemoryMailProfileStore();
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "ses",
            DisplayName = "SES",
            Kind = MailProfileKind.Ses
        });
        await secretStore.SetSecretAsync("ses", MailSecretNames.Password, "retained-secret");

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "ses",
            DisplayName = "SES explicit default",
            Kind = MailProfileKind.Ses,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Region] = "us-east-1"
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("us-east-1", (await service.GetProfileAsync("ses"))!.Settings[MailProfileSettingsKeys.Region]);
        Assert.Equal("retained-secret", await secretStore.GetSecretAsync("ses", MailSecretNames.Password));
    }

    [Fact]
    public async Task SaveAsyncComparesEffectiveProtocolIdentityAcrossFallbackSettings() {
        var store = new InMemoryMailProfileStore();
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "IMAP",
            Kind = MailProfileKind.Imap,
            DefaultMailbox = "reader@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });
        await secretStore.SetSecretAsync("imap", MailSecretNames.Password, "retained-secret");

        OperationResult equivalent = await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "IMAP explicit user",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com",
                [MailProfileSettingsKeys.UserName] = "reader@example.com"
            }
        });
        Assert.True(equivalent.Succeeded);

        OperationResult changed = await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "IMAP changed user",
            Kind = MailProfileKind.Imap,
            DefaultMailbox = "other@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        Assert.False(changed.Succeeded);
        Assert.Equal("profile_credential_context_change_not_allowed", changed.Code);
        Assert.Equal("reader@example.com", (await service.GetProfileAsync("imap"))!.Settings[MailProfileSettingsKeys.UserName]);
        Assert.Equal("retained-secret", await secretStore.GetSecretAsync("imap", MailSecretNames.Password));
    }

    [Fact]
    public async Task InMemoryProfileStoreRejectsProviderKindChangesAtomically() =>
        await AssertProfileStoreRejectsProviderKindChangeAsync(new InMemoryMailProfileStore());

    [Fact]
    public async Task FileProfileStoreRejectsProviderKindChangesAtomically() =>
        await AssertProfileStoreRejectsProviderKindChangeAsync(
            new FileMailProfileStore(CreateTemporaryFilePath("profiles.json")));

    [Fact]
    public async Task InMemoryProfileStoreRejectsCredentialContextChangesAtomically() =>
        await AssertProfileStoreRejectsCredentialContextChangeAsync(new InMemoryMailProfileStore());

    [Fact]
    public async Task FileProfileStoreRejectsCredentialContextChangesAtomically() =>
        await AssertProfileStoreRejectsCredentialContextChangeAsync(
            new FileMailProfileStore(CreateTemporaryFilePath("profiles.json")));

    [Theory]
    [InlineData(false, MailProfileKind.Graph, MailProfileSettingsKeys.TenantId, "tenant-a", "tenant-b")]
    [InlineData(false, MailProfileKind.Gmail, MailProfileSettingsKeys.ClientId, "client-a", "client-b")]
    [InlineData(true, MailProfileKind.Graph, MailProfileSettingsKeys.Mailbox, "user@example.com", "other@example.com")]
    [InlineData(true, MailProfileKind.Gmail, MailProfileSettingsKeys.Mailbox, "user@gmail.com", "other@gmail.com")]
    public async Task ProfileStoresRejectCloudCredentialContextChangesAtomically(
        bool useFileStore,
        MailProfileKind kind,
        string setting,
        string originalValue,
        string changedValue) {
        IMailProfileStore store = useFileStore
            ? new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"))
            : new InMemoryMailProfileStore();
        Dictionary<string, string> originalSettings = CreateValidSettings(kind);
        originalSettings[setting] = originalValue;
        await store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = kind.ToString(),
            Kind = kind,
            Settings = originalSettings
        });
        Dictionary<string, string> changedSettings = CreateValidSettings(kind);
        changedSettings[setting] = changedValue;

        await Assert.ThrowsAsync<MailProfileCredentialContextChangeException>(() => store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Changed",
            Kind = kind,
            Settings = changedSettings
        }));

        Assert.Equal(originalValue, (await store.GetByIdAsync("provider"))!.Settings[setting]);
    }

    private static async Task AssertProfileStoreRejectsProviderKindChangeAsync(IMailProfileStore store) {
        await store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp
        });

        await Assert.ThrowsAsync<MailProfileKindChangeException>(() => store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph
        }));

        Assert.Equal(MailProfileKind.Smtp, (await store.GetByIdAsync("provider"))!.Kind);
    }

    private static async Task AssertProfileStoreRejectsCredentialContextChangeAsync(IMailProfileStore store) {
        await store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "JMAP",
            Kind = MailProfileKind.Jmap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://mail.example.com/.well-known/jmap"
            }
        });

        await Assert.ThrowsAsync<MailProfileCredentialContextChangeException>(() => store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Redirected JMAP",
            Kind = MailProfileKind.Jmap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://mail.attacker.example/.well-known/jmap"
            }
        }));

        Assert.Equal(
            "https://mail.example.com/.well-known/jmap",
            (await store.GetByIdAsync("provider"))!.Settings[MailProfileSettingsKeys.JmapSessionUrl]);
    }

    private static Dictionary<string, string> CreateValidSettings(MailProfileKind kind) =>
        kind switch {
            MailProfileKind.Jmap => new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://mail.example.com/.well-known/jmap"
            },
            MailProfileKind.Graph => new Dictionary<string, string> {
                [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                [MailProfileSettingsKeys.ClientId] = "graph-client",
                [MailProfileSettingsKeys.TenantId] = "tenant-a"
            },
            MailProfileKind.Gmail => new Dictionary<string, string> {
                [MailProfileSettingsKeys.Mailbox] = "user@gmail.com",
                [MailProfileSettingsKeys.ClientId] = "gmail-client"
            },
            _ => new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "mail.example.com"
            }
        };

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class TestCredentialProtector : ICredentialProtector {
        public string Protect(string plainText) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"protected::{plainText}"));

        public string Unprotect(string protectedData) {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedData));
            return decoded.StartsWith("protected::", StringComparison.Ordinal)
                ? decoded.Substring("protected::".Length)
                : decoded;
        }
    }

    private sealed class UnreadableCredentialProtector : ICredentialProtector {
        public string Protect(string plainText) => $"unreadable::{plainText}";

        public string Unprotect(string protectedData) =>
            throw new InvalidDataException("Simulated unreadable protected value.");
    }

    private sealed class FailingCleanupSecretStore : IMailSecretStore, IMailProfileSecretCleanup {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _secrets.TryGetValue($"{profileId}::{secretName}", out string? value);
            return Task.FromResult<string?>(value);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _secrets[$"{profileId}::{secretName}"] = secretValue;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.Remove($"{profileId}::{secretName}"));

        public Task<IReadOnlyDictionary<string, string>> GetProfileSecretsAsync(
            string profileId,
            CancellationToken cancellationToken = default) {
            string prefix = profileId + "::";
            IReadOnlyDictionary<string, string> result = _secrets
                .Where(secret => secret.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    secret => secret.Key.Substring(prefix.Length),
                    secret => secret.Value,
                    StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task RemoveProfileSecretsAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("Simulated secret cleanup failure."));
    }

    private sealed class BasicSecretStore : IMailSecretStore {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _secrets.TryGetValue($"{profileId}::{secretName}", out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _secrets[$"{profileId}::{secretName}"] = secretValue;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.Remove($"{profileId}::{secretName}"));
    }

    private sealed class BlockingCoordinatedSecretStore :
        IMailSecretStore,
        IMailSecretStoreCredentialContextCoordinator {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly TaskCompletionSource<bool> _bindingEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _bindingAllowed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public async Task<string?> GetSecretAsync(
            string profileId,
            string secretName,
            CancellationToken cancellationToken = default) {
            await _gate.WaitAsync(cancellationToken);
            try {
                _secrets.TryGetValue($"{profileId}::{secretName}", out string? value);
                return value;
            } finally {
                _gate.Release();
            }
        }

        public async Task SetSecretAsync(
            string profileId,
            string secretName,
            string secretValue,
            CancellationToken cancellationToken = default) {
            await _gate.WaitAsync(cancellationToken);
            try {
                _secrets[$"{profileId}::{secretName}"] = secretValue;
            } finally {
                _gate.Release();
            }
        }

        public async Task<bool> RemoveSecretAsync(
            string profileId,
            string secretName,
            CancellationToken cancellationToken = default) {
            await _gate.WaitAsync(cancellationToken);
            try {
                return _secrets.Remove($"{profileId}::{secretName}");
            } finally {
                _gate.Release();
            }
        }

        public Task WaitForContextBindingAsync() => _bindingEntered.Task;

        public void AllowContextBinding() => _bindingAllowed.TrySetResult(true);

        async Task<TResult> IMailSecretStoreCredentialContextCoordinator.ExecuteWithProfileSecretsLockedAsync<TResult>(
            string profileId,
            Func<bool, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) {
            await _gate.WaitAsync(cancellationToken);
            try {
                _bindingEntered.TrySetResult(true);
                await _bindingAllowed.Task;
                bool hasSecrets = _secrets.Keys.Any(key =>
                    key.StartsWith(profileId + "::", StringComparison.OrdinalIgnoreCase));
                return await operation(hasSecrets, cancellationToken);
            } finally {
                _gate.Release();
            }
        }
    }
}
