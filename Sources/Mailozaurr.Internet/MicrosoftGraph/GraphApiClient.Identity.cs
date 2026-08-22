using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphApiClient {
    /// <summary>
    /// Gets the provider-verified identity for the selected Graph user.
    /// </summary>
    public async Task<GraphMailboxIdentity> GetMailboxIdentityAsync(
        string userId = "me",
        CancellationToken cancellationToken = default) =>
        await GetMailboxIdentityCoreAsync(userId, refreshOnAuthenticationError: true, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the provider-verified identity without refreshing authentication after a 401/403 response.
    /// </summary>
    /// <remarks>
    /// This is intended for optional capability probes where an authorization denial is evidence and must not mutate
    /// authentication state.
    /// </remarks>
    public async Task<GraphMailboxIdentity> GetMailboxIdentityWithoutRefreshAsync(
        string userId = "me",
        CancellationToken cancellationToken = default) =>
        await GetMailboxIdentityCoreAsync(userId, refreshOnAuthenticationError: false, cancellationToken)
            .ConfigureAwait(false);

    private async Task<GraphMailboxIdentity> GetMailboxIdentityCoreAsync(
        string userId,
        bool refreshOnAuthenticationError,
        CancellationToken cancellationToken) {
        ThrowIfDisposed();

        var userSegment = BuildUserSegment(userId);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            userSegment + "?$select=id,displayName,mail,userPrincipalName");
        ApplyAuthHeader(request);
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (refreshOnAuthenticationError) {
            await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!refreshOnAuthenticationError &&
            (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
             response.StatusCode == System.Net.HttpStatusCode.Forbidden)) {
            throw new GraphApiException(response.StatusCode, "Graph authentication failed.", body);
        }
        if (!response.IsSuccessStatusCode) {
            throw new GraphApiException(
                response.StatusCode,
                $"Graph identity probe failed ({(int)response.StatusCode}).",
                body,
                TryGetRetryAfter(response));
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = TryGetString(root, "id");
        if (string.IsNullOrWhiteSpace(id)) {
            throw new InvalidDataException("Graph returned an identity response without an id.");
        }

        return new GraphMailboxIdentity {
            Id = id!,
            DisplayName = TryGetString(root, "displayName"),
            Mail = TryGetString(root, "mail"),
            UserPrincipalName = TryGetString(root, "userPrincipalName")
        };
    }
}

/// <summary>Identity returned by the Microsoft Graph users endpoint.</summary>
public sealed class GraphMailboxIdentity {
    /// <summary>Graph object identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Primary mail address, when populated.</summary>
    public string? Mail { get; set; }

    /// <summary>User principal name.</summary>
    public string? UserPrincipalName { get; set; }
}
