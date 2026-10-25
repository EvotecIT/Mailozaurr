using System.Diagnostics;
using System.Threading;

namespace Mailozaurr;

public partial class Graph {
    private readonly object _authenticationLock = new();
    private readonly GraphTokenMemoryCache _tokenCache = MicrosoftGraphUtils.MemoryTokenCache;
    private TimeSpan _tokenRefreshSkew = TimeSpan.FromMinutes(5);
    private long _credentialVersion;

    /// <summary>Gets or sets how early cached tokens are refreshed. Defaults to five minutes; zero disables early refresh.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan TokenRefreshSkew {
        get { lock (_authenticationLock) return _tokenRefreshSkew; }
        set {
            if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value));
            lock (_authenticationLock) _tokenRefreshSkew = value;
        }
    }

    /// <summary>Parses clientid@directoryid credentials for in-memory client-secret authentication.</summary>
    /// <param name="Credentials">Client and tenant in the username, with the client secret as the password.</param>
    public void Authenticate(ICredentials Credentials) {
        if (Credentials == null) throw new ArgumentNullException(nameof(Credentials));
        if (Credentials is not NetworkCredential credential) {
            throw new ArgumentException("Credentials must be of type NetworkCredential.", nameof(Credentials));
        }
        string[] parts = credential.UserName.Split('@');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace)) {
            throw new ArgumentException("Credential.UserName must be in the format 'clientid@directoryid'", nameof(Credentials));
        }
        lock (_authenticationLock) {
            ApplicationID = parts[0];
            TenantDomain = parts[1];
            ApplicationKey = credential.Password;
            _credentialVersion++;
            AccessToken = string.Empty;
            TokenType = string.Empty;
        }
    }

    /// <summary>Obtains a client-secret token using only a credential-scoped in-memory cache.</summary>
    /// <param name="cancellationToken">Cancels this attempt, including waiting for another token request.</param>
    /// <returns>The connection result with safe authentication failure details.</returns>
    public async Task<GraphSmtpResult> ConnectO365GraphAsync(CancellationToken cancellationToken = default) {
        Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph authentication.");
            return new GraphSmtpResult(true, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, timer.Elapsed, "Connection skipped (WhatIf)");
        }
        GraphCredential credential;
        TimeSpan skew;
        long version;
        lock (_authenticationLock) {
            credential = new GraphCredential { ClientId = ApplicationID, ClientSecret = ApplicationKey, DirectoryId = TenantDomain };
            skew = _tokenRefreshSkew;
            version = _credentialVersion;
        }
        try {
            if (string.IsNullOrWhiteSpace(credential.ClientId) || string.IsNullOrWhiteSpace(credential.DirectoryId) ||
                string.IsNullOrEmpty(credential.ClientSecret)) throw new InvalidOperationException();
            GraphAuthorization authorization = await _tokenCache.GetAsync(credential, credential.DirectoryId,
                "https://graph.microsoft.com", _client, skew, cancellationToken).ConfigureAwait(false);
            lock (_authenticationLock) {
                if (version != _credentialVersion) throw new InvalidOperationException();
                AccessToken = authorization.AccessToken;
                TokenType = authorization.TokenType;
            }
            return new GraphSmtpResult(true, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, timer.Elapsed, string.Empty, string.Empty);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException("Microsoft Graph authentication canceled.", cancellationToken);
        } catch (Exception ex) {
            lock (_authenticationLock) {
                if (version == _credentialVersion) {
                    AccessToken = string.Empty;
                    TokenType = string.Empty;
                }
            }
            const string message = MicrosoftGraphUtils.AuthenticationFailure;
            LogCollector.LogWarning(message);
            if (ErrorAction == ActionPreference.Stop) {
                if (ex is GraphApiException graphException) {
                    throw new GraphApiException(graphException.StatusCode, message, string.Empty);
                }
                throw new InvalidOperationException(message);
            }
            return new GraphSmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, timer.Elapsed, string.Empty, message) {
                GraphError = GraphApiErrorParser.Parse(message, (ex as GraphApiException)?.StatusCode)
            };
        }
    }
}
