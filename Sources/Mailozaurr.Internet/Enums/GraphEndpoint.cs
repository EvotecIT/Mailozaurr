namespace Mailozaurr;

/// <summary>
/// Known Microsoft Graph API endpoints.
/// </summary>
/// <remarks>
/// These values are used when constructing Graph URLs for
/// requests to either the stable or beta API surface.
/// </remarks>
public enum GraphEndpoint {
    /// <summary>Represents the v1.0 endpoint.</summary>
    V1,
    /// <summary>Represents the beta endpoint.</summary>
    Beta
}