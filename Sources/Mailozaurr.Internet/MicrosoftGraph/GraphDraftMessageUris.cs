namespace Mailozaurr;

/// <summary>
/// Builds Microsoft Graph draft-message action URIs used by large attachment sends.
/// </summary>
public static class GraphDraftMessageUris {
    /// <summary>
    /// Builds the URI used to create an upload session for a draft message attachment.
    /// </summary>
    /// <param name="userPrincipalName">Mailbox user principal name or SMTP address.</param>
    /// <param name="draftMessageId">Graph draft message identifier.</param>
    /// <returns>The absolute Microsoft Graph upload-session URI.</returns>
    public static string CreateUploadSession(string userPrincipalName, string draftMessageId) =>
        BuildDraftActionUri(userPrincipalName, draftMessageId, "attachments", "createUploadSession");

    /// <summary>
    /// Builds the URI used to add a small file attachment directly to a draft message.
    /// </summary>
    public static string Attachments(string userPrincipalName, string draftMessageId) =>
        BuildDraftActionUri(userPrincipalName, draftMessageId, "attachments");

    /// <summary>
    /// Builds the URI used to send an existing draft message.
    /// </summary>
    /// <param name="userPrincipalName">Mailbox user principal name or SMTP address.</param>
    /// <param name="draftMessageId">Graph draft message identifier.</param>
    /// <returns>The absolute Microsoft Graph draft-send URI.</returns>
    public static string Send(string userPrincipalName, string draftMessageId) =>
        BuildDraftActionUri(userPrincipalName, draftMessageId, "send");

    private static string BuildDraftActionUri(
        string userPrincipalName,
        string draftMessageId,
        params string[] actionSegments) {
        if (string.IsNullOrWhiteSpace(userPrincipalName)) {
            throw new ArgumentException("User principal name must be provided.", nameof(userPrincipalName));
        }
        if (string.IsNullOrWhiteSpace(draftMessageId)) {
            throw new ArgumentException("Draft message id must be provided.", nameof(draftMessageId));
        }

        var segments = new string[4 + actionSegments.Length];
        segments[0] = "users";
        segments[1] = userPrincipalName;
        segments[2] = "messages";
        segments[3] = draftMessageId;
        Array.Copy(actionSegments, 0, segments, 4, actionSegments.Length);
        return MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            MicrosoftGraphUtils.BuildGraphPath(segments));
    }
}
