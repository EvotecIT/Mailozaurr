using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using Mailozaurr;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileConnectionServiceTests {
    [Fact]
    public void OriginalConstructorRemainsUnambiguousForPositionalNullArguments() {
        var profileStore = new InMemoryProfileStore(Array.Empty<MailProfile>());

        var service = new MailProfileConnectionService(profileStore, null, null, null, null, null);

        Assert.NotNull(service);
    }

    [Fact]
    public void Pop3FactoryIsPublicForDirectLibraryComposition() {
        var method = typeof(MailProfileConnectionService).GetMethod(
            nameof(MailProfileConnectionService.CreateWithPop3));

        Assert.NotNull(method);
        Assert.True(method!.IsPublic);
    }

    [Fact]
    public async Task TestAsyncUsesMailboxScopeByDefaultForGmail() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "user@gmail.com"
            }
        });
        var authProbeCalls = 0;
        var mailboxProbeCalls = 0;
        var service = new MailProfileConnectionService(
            profileStore,
            gmailSessionFactory: new FakeGmailSessionFactory(),
            probeGmailAsync: (_, _) => {
                authProbeCalls++;
                return Task.CompletedTask;
            },
            probeGmailMailboxAsync: (session, _) => {
                mailboxProbeCalls++;
                Assert.Equal("user@gmail.com", session.UserId);
                return Task.CompletedTask;
            });

        var result = await service.TestAsync("gmail-work");

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileConnectionTestScope.Auto, result.RequestedScope);
        Assert.Equal(MailProfileConnectionTestScope.Mailbox, result.ExecutedScope);
        Assert.Equal("listFolders", result.Probe);
        Assert.Equal(0, authProbeCalls);
        Assert.Equal(1, mailboxProbeCalls);
        Assert.Collection(
            result.Stages,
            stage => {
                Assert.Equal(MailProfileConnectionTestPhase.Profile, stage.Phase);
                Assert.True(stage.Succeeded);
            },
            stage => {
                Assert.Equal(MailProfileConnectionTestPhase.Session, stage.Phase);
                Assert.True(stage.Succeeded);
            },
            stage => {
                Assert.Equal(MailProfileConnectionTestPhase.Mailbox, stage.Phase);
                Assert.True(stage.Succeeded);
                Assert.Equal("listFolders", stage.Probe);
            });
    }

    [Fact]
    public async Task DefaultGmailAuthProbeReportsVerifiedIdentityCountsAndHistoryCursor() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "user@gmail.com"
            }
        });
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"emailAddress\":\"user@gmail.com\",\"messagesTotal\":42,\"threadsTotal\":21,\"historyId\":\"9001\"}")
        });
        var service = new MailProfileConnectionService(
            profileStore,
            gmailSessionFactory: new HttpGmailSessionFactory(handler));

        var result = await service.TestAsync("gmail-work", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Equal("Gmail", evidence?.Protocol);
        Assert.Equal("user@gmail.com", evidence?.Identity?.EmailAddress);
        Assert.Equal("Gmail users.getProfile endpoint", evidence?.Identity?.Source);
        Assert.Equal(42, evidence?.Mailbox?.MessageCount);
        Assert.Equal(21, evidence?.Mailbox?.ThreadCount);
        Assert.Equal("9001", evidence?.Mailbox?.ChangeCursor);
        Assert.Equal("unavailable", evidence?.Permissions?.Source);
        Assert.Empty(evidence?.Permissions?.Names ?? new List<string>());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TestAsyncUsesAuthScopeWhenRequestedForGraph() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-work",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultMailbox = "user@example.com"
            }
        });
        var authProbeCalls = 0;
        var mailboxProbeCalls = 0;
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new FakeGraphSessionFactory(),
            probeGraphAsync: (_, _) => {
                authProbeCalls++;
                return Task.CompletedTask;
            },
            probeGraphMailboxAsync: (_, _) => {
                mailboxProbeCalls++;
                return Task.CompletedTask;
            });

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileConnectionTestScope.Auth, result.RequestedScope);
        Assert.Equal(MailProfileConnectionTestScope.Auth, result.ExecutedScope);
        Assert.Equal("inspectMailAccess", result.Probe);
        Assert.Equal(1, authProbeCalls);
        Assert.Equal(0, mailboxProbeCalls);
    }

    [Fact]
    public async Task DefaultGraphAuthProbeVerifiesIdentityAndReportsTokenDeclaredPermissions() {
        var profileStore = CreateGraphProfileStore();
        var handler = new RecordingHandler(
            JsonResponse("{\"id\":\"user-id\",\"displayName\":\"Ada Lovelace\",\"mail\":\"ada@example.com\",\"userPrincipalName\":\"ada@example.com\"}"));
        var credential = new OAuthCredential {
            UserName = "ada@example.com",
            AccessToken = CreateJwt("{\"scp\":\"Mail.Read Mail.Send\",\"roles\":[\"MailboxSettings.Read\"]}"),
            ExpiresOn = DateTimeOffset.MaxValue
        };
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new HttpGraphSessionFactory(handler, credential));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        var probe = result.Stages[result.Stages.Count - 1];
        Assert.Equal("user-id", probe.Evidence?.Identity?.Id);
        Assert.Equal("ada@example.com", probe.Evidence?.Identity?.EmailAddress);
        Assert.Equal("Microsoft Graph users endpoint", probe.Evidence?.Identity?.Source);
        Assert.False(probe.Evidence?.Permissions?.Authoritative);
        Assert.Equal(
            new[] { "Mail.Read", "Mail.Send", "MailboxSettings.Read" },
            probe.Evidence?.Permissions?.Names);
        Assert.Equal(new[] { "Mail.Read", "Mail.Send" }, probe.Evidence?.Permissions?.DelegatedScopes);
        Assert.Equal(new[] { "MailboxSettings.Read" }, probe.Evidence?.Permissions?.ApplicationRoles);
        Assert.Single(handler.Requests);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/ada%40example.com?$select=id,displayName,mail,userPrincipalName",
            handler.Requests[0].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task DefaultGraphAuthProbeKeepsMailEvidenceWhenIdentityPermissionIsUnavailable() {
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt("{\"scp\":\"Mail.ReadWrite Mail.Send\"}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Null(evidence?.Identity);
        Assert.Contains("403", evidence?.IdentityUnavailableReason);
        Assert.Equal(new[] { "Mail.ReadWrite", "Mail.Send" }, evidence?.Permissions?.Names);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DefaultGraphAuthProbeAcceptsRecognizedMailSendCredentialWithoutMailboxRead() {
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt("{\"scp\":\"Mail.Send\"}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Equal(new[] { "Mail.Send" }, evidence?.Permissions?.DelegatedScopes);
        Assert.Contains("403", evidence?.IdentityUnavailableReason);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain(handler.Requests, request =>
            request.RequestUri?.AbsoluteUri.Contains("/mailFolders", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task DefaultGraphAuthProbeDoesNotCallMeForApplicationTokenWithoutMailbox() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-app",
                DisplayName = "Graph application",
                Kind = MailProfileKind.Graph
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "app",
                AccessToken = CreateJwt("{\"roles\":[\"Mail.Read\"]}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-app", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Equal(new[] { "Mail.Read" }, evidence?.Permissions?.ApplicationRoles);
        Assert.Null(evidence?.Identity);
        Assert.Contains("authenticated", evidence?.IdentityUnavailableReason);
        Assert.Single(handler.Requests);
        Assert.Contains("/organization?", handler.Requests[0].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task DefaultGraphAuthProbeDoesNotCallMeForOpaqueApplicationTokenWithoutMailbox() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-app",
                DisplayName = "Graph application",
                Kind = MailProfileKind.Graph
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new HttpGraphSessionFactory(
                handler,
                new OAuthCredential {
                    UserName = "app",
                    AccessToken = "opaque-application-token",
                    ExpiresOn = DateTimeOffset.MaxValue
                },
                graphCredential: new GraphCredential {
                    ClientId = "client-id",
                    DirectoryId = "tenant-id",
                    ClientSecret = "client-secret"
                }));

        var result = await service.TestAsync("graph-app", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Empty(evidence?.Permissions?.ApplicationRoles ?? new List<string>());
        Assert.Null(evidence?.Identity);
        Assert.Contains("authenticated", evidence?.IdentityUnavailableReason);
        Assert.Single(handler.Requests);
        Assert.Contains("/organization?", handler.Requests[0].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task DefaultGraphAuthProbeDoesNotCallMeForOpaqueAccessTokenOnlySessionWithoutMailbox() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-token",
                DisplayName = "Graph token",
                Kind = MailProfileKind.Graph
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "opaque",
                AccessToken = "opaque-access-token",
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-token", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Null(evidence?.Identity);
        Assert.Contains("mode remained unknown", evidence?.IdentityUnavailableReason);
        Assert.Single(handler.Requests);
        Assert.Contains("/organization?", handler.Requests[0].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task DefaultGraphAuthProbeRejectsInvalidOpaqueApplicationTokenWithoutMailbox() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-app",
                DisplayName = "Graph application",
                Kind = MailProfileKind.Graph
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"InvalidAuthenticationToken\"}}", HttpStatusCode.Unauthorized));
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new HttpGraphSessionFactory(
                handler,
                new OAuthCredential {
                    UserName = "app",
                    AccessToken = "invalid-opaque-application-token",
                    ExpiresOn = DateTimeOffset.MaxValue
                },
                graphCredential: new GraphCredential {
                    ClientId = "client-id",
                    DirectoryId = "tenant-id",
                    ClientSecret = "client-secret"
                }));

        var result = await service.TestAsync("graph-app", MailProfileConnectionTestScope.Auth);

        Assert.False(result.Succeeded);
        Assert.Equal("connection_test_failed", result.Code);
        Assert.Single(handler.Requests);
        Assert.Contains("/organization?", handler.Requests[0].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task DefaultGraphSendPreflightFailsWhenDraftCreationPermissionIsMissing() {
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var refreshCalls = 0;
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt("{\"scp\":\"Mail.Send\"}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }, _ => {
                refreshCalls++;
                return Task.FromResult("refreshed-token");
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Send);

        Assert.False(result.Succeeded);
        Assert.Equal("send_preflight_not_ready", result.Code);
        var stage = result.Stages[result.Stages.Count - 1];
        Assert.False(stage.Succeeded);
        Assert.False(stage.Evidence?.Preflight?.Ready);
        Assert.Contains("Mail.ReadWrite", stage.Evidence?.Preflight?.Detail);
        Assert.Equal("denied-mail-endpoint-and-token-claims", stage.Evidence?.Preflight?.ValidationLevel);
        Assert.Equal(new[] { "Mail.Send" }, stage.Evidence?.Permissions?.DelegatedScopes);
        Assert.Equal(0, refreshCalls);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DefaultGraphSendPreflightAcceptsDelegatedDirectPairOnlyForSignedInMailbox() {
        var handler = new RecordingHandler(
            JsonResponse("{\"value\":[]}"),
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt("{\"scp\":\"Mail.ReadWrite Mail.Send\",\"preferred_username\":\"ada@example.com\"}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.True(evidence?.Preflight?.Ready);
        Assert.Equal("ada@example.com", evidence?.Permissions?.DelegatedIdentity);
    }

    [Fact]
    public async Task DefaultGraphSendPreflightRecognizesDelegatedObjectIdMailbox() {
        const string objectId = "11111111-2222-3333-4444-555555555555";
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-work",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultMailbox = objectId
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"value\":[]}"),
            JsonResponse($"{{\"id\":\"{objectId}\",\"mail\":\"ada@example.com\",\"userPrincipalName\":\"ada@example.com\"}}"));
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt($"{{\"scp\":\"Mail.ReadWrite Mail.Send\",\"oid\":\"{objectId}\"}}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        var permissions = result.Stages[result.Stages.Count - 1].Evidence?.Permissions;
        Assert.True(result.Stages[result.Stages.Count - 1].Evidence?.Preflight?.Ready);
        Assert.Equal(objectId, permissions?.DelegatedObjectId);
        Assert.Contains(objectId, permissions?.DelegatedMailboxIdentifiers ?? new List<string>());
        Assert.Contains("ada@example.com", permissions?.DelegatedMailboxIdentifiers ?? new List<string>());
    }

    [Fact]
    public async Task DefaultGraphSendPreflightUsesVerifiedIdentityAliasForDelegatedObjectId() {
        const string objectId = "11111111-2222-3333-4444-555555555555";
        var handler = new RecordingHandler(
            JsonResponse("{\"value\":[]}"),
            JsonResponse($"{{\"id\":\"{objectId}\",\"mail\":\"ada@example.com\",\"userPrincipalName\":\"ada@example.com\"}}"));
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt($"{{\"scp\":\"Mail.ReadWrite Mail.Send\",\"oid\":\"{objectId}\"}}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        Assert.True(result.Stages[result.Stages.Count - 1].Evidence?.Preflight?.Ready);
    }

    [Fact]
    public async Task DefaultGraphSendPreflightRejectsDirectPairForSelectedSharedMailbox() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-work",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultMailbox = "shared@example.com"
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"value\":[]}"),
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "owner@example.com",
                AccessToken = CreateJwt("{\"scp\":\"Mail.ReadWrite Mail.Send Mail.ReadWrite.Shared\",\"preferred_username\":\"owner@example.com\"}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Send);

        Assert.False(result.Succeeded);
        Assert.Equal("send_preflight_not_ready", result.Code);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.False(evidence?.Preflight?.Ready);
        Assert.Contains("differs", evidence?.Preflight?.Detail);
    }

    [Fact]
    public async Task DefaultGraphSendPreflightKeepsSharedMailboxReadinessUnknownWithoutExchangeDelegationEvidence() {
        var handler = new RecordingHandler(
            JsonResponse("{\"value\":[]}"),
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt("{\"scp\":\"Mail.ReadWrite.Shared Mail.Send.Shared\"}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        var preflight = result.Stages[result.Stages.Count - 1].Evidence?.Preflight;
        Assert.Null(preflight?.Ready);
        Assert.Contains("Send As or Send on Behalf", preflight?.Detail);
    }

    [Fact]
    public async Task DefaultGraphSendPreflightAcceptsApplicationRolePairForSelectedMailbox() {
        var handler = new RecordingHandler(
            JsonResponse("{\"value\":[]}"),
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "app",
                AccessToken = CreateJwt("{\"roles\":[\"Mail.ReadWrite\",\"Mail.Send\"]}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.True(evidence?.Preflight?.Ready);
        Assert.Empty(evidence?.Permissions?.DelegatedScopes ?? new List<string>());
        Assert.Equal(new[] { "Mail.ReadWrite", "Mail.Send" }, evidence?.Permissions?.ApplicationRoles);
    }

    [Fact]
    public async Task DefaultGmailMailboxProbeDoesNotRequireProfilePermission() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "user@gmail.com"
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"labels\":[{\"id\":\"INBOX\",\"name\":\"INBOX\",\"type\":\"system\"}]}"),
            JsonResponse(GmailInsufficientPermissionsResponse, HttpStatusCode.Forbidden));
        var refreshCalls = 0;
        var service = new MailProfileConnectionService(
            profileStore,
            gmailSessionFactory: new HttpGmailSessionFactory(handler, _ => {
                refreshCalls++;
                return Task.FromResult("refreshed-token");
            }));

        var result = await service.TestAsync("gmail-work", MailProfileConnectionTestScope.Mailbox);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Equal(1, evidence?.Mailbox?.FolderCount);
        Assert.Null(evidence?.Identity);
        Assert.Contains("403", evidence?.IdentityUnavailableReason);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/labels?", handler.Requests[0].RequestUri?.AbsoluteUri);
        Assert.EndsWith("/profile", handler.Requests[1].RequestUri?.AbsoluteUri);
        Assert.Equal(0, refreshCalls);
    }

    [Fact]
    public async Task DefaultGmailSendPreflightKeepsReadinessUnknownForSendOnlyCredential() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "user@gmail.com"
            }
        });
        var handler = new RecordingHandler(
            JsonResponse(GmailInsufficientPermissionsResponse, HttpStatusCode.Forbidden));
        var refreshCalls = 0;
        var service = new MailProfileConnectionService(
            profileStore,
            gmailSessionFactory: new HttpGmailSessionFactory(handler, _ => {
                refreshCalls++;
                return Task.FromResult("refreshed-token");
            }));

        var result = await service.TestAsync("gmail-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        var evidence = result.Stages[result.Stages.Count - 1].Evidence;
        Assert.Null(evidence?.Preflight?.Ready);
        Assert.Contains("outside its granted scope", evidence?.Permissions?.Detail);
        Assert.Contains("403", evidence?.IdentityUnavailableReason);
        Assert.Equal(0, refreshCalls);
    }

    [Fact]
    public async Task DefaultGmailProbeDoesNotSuppressNonScopeForbiddenResponses() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "user@gmail.com"
            }
        });
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"errors\":[{\"reason\":\"accessNotConfigured\"}],\"code\":403}}", HttpStatusCode.Forbidden));
        var service = new MailProfileConnectionService(
            profileStore,
            gmailSessionFactory: new HttpGmailSessionFactory(handler));

        var result = await service.TestAsync("gmail-work", MailProfileConnectionTestScope.Send);

        Assert.False(result.Succeeded);
        Assert.Equal("connection_test_failed", result.Code);
    }

    [Fact]
    public async Task DefaultGraphIdentityProbePropagatesUnauthorizedResponse() {
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"InvalidAuthenticationToken\"}}", HttpStatusCode.Unauthorized));
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = CreateJwt("{\"scp\":\"Mail.Read\",\"preferred_username\":\"ada@example.com\"}"),
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.False(result.Succeeded);
        Assert.Equal("connection_test_failed", result.Code);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DefaultGraphOptionalIdentityDenialDoesNotRefreshCredential() {
        var handler = new RecordingHandler(
            JsonResponse("{\"error\":{\"code\":\"Authorization_RequestDenied\"}}", HttpStatusCode.Forbidden));
        var refreshCalls = 0;
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(
                handler,
                new OAuthCredential {
                    UserName = "ada@example.com",
                    AccessToken = CreateJwt("{\"scp\":\"Mail.Read\",\"preferred_username\":\"ada@example.com\"}"),
                    ExpiresOn = DateTimeOffset.MaxValue
                },
                _ => {
                    refreshCalls++;
                    return Task.FromResult("refreshed-token");
                }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        Assert.Contains("403", result.Stages[result.Stages.Count - 1].Evidence?.IdentityUnavailableReason);
        Assert.Equal(0, refreshCalls);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, 0L)]
    [InlineData(7, 7L)]
    public void ImapUnreadEvidencePreservesUnknownAsNull(int unread, long? expected) {
        Assert.Equal(expected, MailProfileConnectionService.NormalizeImapUnreadCount(unread));
    }

    [Fact]
    public async Task DefaultGraphAuthProbeDoesNotTreatConfiguredUserIdAsAuthenticationProof() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized) {
            Content = new StringContent("{\"error\":{\"code\":\"InvalidAuthenticationToken\"}}")
        });
        var service = new MailProfileConnectionService(
            CreateGraphProfileStore(),
            graphSessionFactory: new HttpGraphSessionFactory(handler, new OAuthCredential {
                UserName = "ada@example.com",
                AccessToken = "opaque-token",
                ExpiresOn = DateTimeOffset.MaxValue
            }));

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.False(result.Succeeded);
        Assert.Equal("connection_test_failed", result.Code);
        Assert.Equal(MailProfileConnectionTestPhase.Probe, result.Stages[result.Stages.Count - 1].Phase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TestAsyncFallsBackFromSendToAuthForImap() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "imap-work",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                DefaultMailbox = "user@example.com"
            }
        });
        var service = new MailProfileConnectionService(
            profileStore,
            imapSessionFactory: new FakeImapSessionFactory(),
            probeImapAsync: (_, _) => Task.CompletedTask,
            probeImapMailboxAsync: (_, _) => Task.CompletedTask);

        var result = await service.TestAsync("imap-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileConnectionTestScope.Send, result.RequestedScope);
        Assert.Equal(MailProfileConnectionTestScope.Auth, result.ExecutedScope);
        Assert.Equal("connect", result.Probe);
    }

    [Fact]
    public async Task TestAsyncUsesMailboxScopeByDefaultForPop3() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "pop3-work",
                DisplayName = "Work POP3",
                Kind = MailProfileKind.Pop3,
                DefaultMailbox = "user@example.com"
            }
        });
        var mailboxProbeCalls = 0;
        var service = MailProfileConnectionService.CreateWithPop3(
            profileStore,
            new FakePop3SessionFactory(),
            imapSessionFactory: null,
            graphSessionFactory: null,
            gmailSessionFactory: null,
            smtpSessionFactory: null,
            probePop3Async: (_, _) => Task.CompletedTask,
            probePop3MailboxAsync: (_, _) => {
                mailboxProbeCalls++;
                return Task.CompletedTask;
            });

        var result = await service.TestAsync("pop3-work");

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileKind.Pop3, result.ProfileKind);
        Assert.Equal(MailProfileConnectionTestScope.Mailbox, result.ExecutedScope);
        Assert.Equal("inspectMailbox", result.Probe);
        Assert.Equal(1, mailboxProbeCalls);
        Assert.Equal(MailProfileConnectionTestPhase.Mailbox, result.Stages[result.Stages.Count - 1].Phase);
    }

    [Fact]
    public async Task TestAsyncRecordsFailedProbeWithoutLosingCompletedStages() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "imap-work",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                DefaultMailbox = "user@example.com"
            }
        });
        var service = new MailProfileConnectionService(
            profileStore,
            imapSessionFactory: new FakeImapSessionFactory(),
            probeImapAsync: (_, _) => throw new InvalidOperationException("Authentication probe failed."));

        var result = await service.TestAsync("imap-work", MailProfileConnectionTestScope.Auth);

        Assert.False(result.Succeeded);
        Assert.Equal("connection_test_failed", result.Code);
        Assert.Collection(
            result.Stages,
            stage => Assert.Equal(MailProfileConnectionTestPhase.Profile, stage.Phase),
            stage => Assert.Equal(MailProfileConnectionTestPhase.Session, stage.Phase),
            stage => {
                Assert.Equal(MailProfileConnectionTestPhase.Probe, stage.Phase);
                Assert.False(stage.Succeeded);
                Assert.Equal("connection_test_failed", stage.Code);
            });
    }

    [Fact]
    public async Task TestAsyncConvertsSessionDisposalFailureIntoStructuredResult() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "imap-work",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                DefaultMailbox = "user@example.com"
            }
        });
        var service = new MailProfileConnectionService(
            profileStore,
            imapSessionFactory: new ThrowingDisposeImapSessionFactory(),
            probeImapAsync: (_, _) => Task.CompletedTask);

        var result = await service.TestAsync("imap-work", MailProfileConnectionTestScope.Auth);

        Assert.False(result.Succeeded);
        Assert.Equal("connection_test_failed", result.Code);
        var cleanup = result.Stages[result.Stages.Count - 1];
        Assert.Equal(MailProfileConnectionTestPhase.Cleanup, cleanup.Phase);
        Assert.Equal("dispose", cleanup.Probe);
        Assert.Contains("dispose failed", cleanup.Message);
    }

    private sealed class InMemoryProfileStore : IMailProfileStore {
        private readonly Dictionary<string, MailProfile> _profiles;

        public InMemoryProfileStore(IEnumerable<MailProfile> profiles) {
            _profiles = profiles.ToDictionary(profile => profile.Id, CloneProfile, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(_profiles.Values.Select(CloneProfile).ToArray());

        public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
            _profiles.TryGetValue(profileId, out var profile);
            return Task.FromResult(profile == null ? null : CloneProfile(profile));
        }

        public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Remove(profileId));

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            _profiles[profile.Id] = CloneProfile(profile);
            return Task.CompletedTask;
        }

        private static MailProfile CloneProfile(MailProfile profile) => new() {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            Kind = profile.Kind,
            DefaultSender = profile.DefaultSender,
            DefaultMailbox = profile.DefaultMailbox,
            IsDefault = profile.IsDefault,
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase),
            Capabilities = profile.Capabilities == null
                ? null
                : new ProfileCapabilities(profile.Capabilities.Kind, profile.Capabilities.Capabilities)
        };
    }

    private static InMemoryProfileStore CreateGraphProfileStore() => new(new[] {
        new MailProfile {
            Id = "graph-work",
            DisplayName = "Work Graph",
            Kind = MailProfileKind.Graph,
            DefaultMailbox = "ada@example.com"
        }
    });

    private const string GmailInsufficientPermissionsResponse =
        "{\"error\":{\"errors\":[{\"reason\":\"insufficientPermissions\"}],\"code\":403,\"status\":\"PERMISSION_DENIED\"}}";

    private static string CreateJwt(string payload) {
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return Encode("{\"alg\":\"none\"}") + "." + Encode(payload) + ".signature";
    }

    private static HttpResponseMessage JsonResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode) {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class FakeImapSessionFactory : IImapSessionFactory {
        public Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImapClient());
    }

    private sealed class ThrowingDisposeImapSessionFactory : IImapSessionFactory {
        public Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult<ImapClient>(new ThrowingDisposeImapClient());
    }

    private sealed class ThrowingDisposeImapClient : ImapClient {
        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (disposing) {
                throw new InvalidOperationException("dispose failed");
            }
        }
    }

    private sealed class FakePop3SessionFactory : IPop3SessionFactory {
        public Task<Pop3Client> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Pop3Client());
    }

    private sealed class FakeGraphSessionFactory : IGraphSessionFactory {
        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GraphSession(new GraphApiClient(new OAuthCredential {
                UserName = profile.DefaultMailbox ?? "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            }), profile.DefaultMailbox ?? "me"));
    }

    private sealed class HttpGraphSessionFactory : IGraphSessionFactory {
        private readonly HttpMessageHandler _handler;
        private readonly OAuthCredential _credential;
        private readonly Func<CancellationToken, Task<string>>? _refreshToken;
        private readonly GraphCredential? _graphCredential;

        public HttpGraphSessionFactory(
            HttpMessageHandler handler,
            OAuthCredential credential,
            Func<CancellationToken, Task<string>>? refreshToken = null,
            GraphCredential? graphCredential = null) {
            _handler = handler;
            _credential = credential;
            _refreshToken = refreshToken;
            _graphCredential = graphCredential;
        }

        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            var httpClient = new HttpClient(_handler) {
                BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
            };
            var client = new GraphApiClient(httpClient, _refreshToken, credential: _credential);
            return Task.FromResult(new GraphSession(client, profile.DefaultMailbox ?? "me", _credential, _graphCredential));
        }
    }

    private sealed class FakeGmailSessionFactory : IGmailSessionFactory {
        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GmailSession(new GmailApiClient(new OAuthCredential {
                UserName = profile.DefaultMailbox ?? "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            }), profile.DefaultMailbox ?? "me"));
    }

    private sealed class HttpGmailSessionFactory : IGmailSessionFactory {
        private readonly HttpMessageHandler _handler;
        private readonly Func<CancellationToken, Task<string>>? _refreshToken;

        public HttpGmailSessionFactory(
            HttpMessageHandler handler,
            Func<CancellationToken, Task<string>>? refreshToken = null) {
            _handler = handler;
            _refreshToken = refreshToken;
        }

        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            var httpClient = new HttpClient(_handler) {
                BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
            };
            var client = new GmailApiClient(httpClient, _refreshToken);
            return Task.FromResult(new GmailSession(client, profile.DefaultMailbox ?? "me"));
        }
    }
}
