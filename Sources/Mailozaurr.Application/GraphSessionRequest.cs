namespace Mailozaurr.Application;

/// <summary>
/// Represents the resolved session input required to connect to Microsoft Graph.
/// </summary>
public sealed class GraphSessionRequest {
    /// <summary>Resolved Graph mailbox user id.</summary>
    public string UserId { get; set; } = "me";

    /// <summary>OAuth credential used to authenticate Graph requests.</summary>
    public OAuthCredential Credential { get; set; } = new();

    /// <summary>Optional Graph credential metadata used to mint new access tokens.</summary>
    public GraphCredential? GraphCredential { get; set; }
}