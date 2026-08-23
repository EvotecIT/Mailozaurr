namespace Mailozaurr;

/// <summary>Creates authenticated JMAP sessions from stored profiles.</summary>
public interface IJmapSessionFactory {
    /// <summary>Connects a JMAP profile.</summary>
    Task<JmapSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default);
}

/// <summary>Inputs used to create one JMAP session.</summary>
public sealed class JmapSessionRequest {
    /// <summary>Validated HTTPS Session resource URL.</summary>
    public Uri SessionUrl { get; set; } = null!;

    /// <summary>Bearer access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Optional account override.</summary>
    public string? AccountId { get; set; }

    /// <summary>Whether this profile explicitly authorizes cross-origin JMAP discovery and API URLs.</summary>
    public bool AllowCrossOriginApiUrl { get; set; }
}

/// <summary>Connected JMAP client and optional account selection.</summary>
public sealed class JmapSession : IDisposable {
    /// <summary>Creates a JMAP session.</summary>
    public JmapSession(JmapApiClient client, string? accountId = null) {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        AccountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }

    /// <summary>JMAP protocol client.</summary>
    public JmapApiClient Client { get; }

    /// <summary>Optional selected account identifier.</summary>
    public string? AccountId { get; }

    /// <inheritdoc />
    public void Dispose() => Client.Dispose();
}
