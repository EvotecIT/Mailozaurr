using System.Runtime.CompilerServices;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

public partial class Smtp {
    // Callback identity is process-local, like the connection pool. Weak keys avoid
    // retaining application callbacks after their transport has left the pool.
    private static readonly ConditionalWeakTable<RemoteCertificateValidationCallback, CallbackIdentity> CallbackIdentities = new();
    private static long nextCallbackIdentity;
    private sealed class CallbackIdentity {
        internal readonly long Value = Interlocked.Increment(ref nextCallbackIdentity);
    }
    private static readonly RemoteCertificateValidationCallback AcceptAnyCertificate = (_, _, _, _) => true;
    private string? connectedPoolPolicy;
    private bool reusableAuthentication = true;
    private ProtocolAuthMode credentialAuthMode;
    private static readonly byte[] PoolCredentialKey = CreatePoolCredentialKey();

    /// <summary>Supplies the authentication context used to select a pooled connection before calling Connect.</summary>
    /// <remarks>This configures credentials; it does not authenticate. Call Authenticate after Connect, or use ConnectAndAuthenticateAsync.</remarks>
    /// <param name="credential">Account credentials for the connection.</param>
    /// <param name="mode">Basic or OAuth2 authentication.</param>
    public void ConfigureAuthentication(NetworkCredential credential, ProtocolAuthMode mode = ProtocolAuthMode.Basic) {
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        if (mode != ProtocolAuthMode.Basic && mode != ProtocolAuthMode.OAuth2) throw new ArgumentOutOfRangeException(nameof(mode));
        Credential = new NetworkCredential(credential.UserName, credential.Password, credential.Domain);
        credentialAuthMode = mode;
    }

    private static byte[] CreatePoolCredentialKey() {
        var key = new byte[32];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(key);
        return key;
    }

    private static string GetCredentialFingerprint(NetworkCredential credential) {
        var user = credential.UserName;
        var domain = credential.Domain;
        var secret = credential.Password;
        var bytes = Encoding.UTF8.GetBytes($"{user.Length}:{user}{domain.Length}:{domain}{secret.Length}:{secret}");
        try {
            using var hash = new HMACSHA256(PoolCredentialKey);
            return Convert.ToBase64String(hash.ComputeHash(bytes));
        } finally {
            Array.Clear(bytes, 0, bytes.Length);
        }
    }

    private bool ReuseAuthentication(NetworkCredential? credential) {
        if (credential != null && !SmtpValidation.TryValidateCredentials(credential.UserName, credential.Password, out var error))
            throw new InvalidOperationException(error);
        if (!Client.IsAuthenticated) return false;
        if (credential == null || !string.Equals(_poolIdentity, GetPoolUserIdentity(credential) + "|" + connectedPoolPolicy, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The SMTP connection is already authenticated for a different or unknown identity.");
        }
        Credential = credential;
        return true;
    }

    private void RecordAuthenticationIdentity() {
        reusableAuthentication = Credential != null;
        if (reusableAuthentication) _poolIdentity = GetPoolUserIdentity(Credential) + "|" + connectedPoolPolicy;
    }

    private void ReturnConnectionToPool(string server, int port, string identity) {
        var transport = Client;
        var replacement = ClientFactory(Logging?.ProtocolLogger);
        if (ReferenceEquals(transport, replacement)) replacement = CreateDefaultClient(Logging?.ProtocolLogger);
        CopyClientState(transport, replacement);
        ClearMessageState(transport);
        Client = replacement;
        var allowReuse = reusableAuthentication && (!transport.IsAuthenticated || Credential != null);
        SmtpConnectionPool.ReturnClient(server, port, transport, identity, IsConnectionPoolingEnabled && allowReuse);
    }

    private void AdoptPooledClient(ClientSmtp transport) {
        var previous = Client;
        if (ReferenceEquals(previous, transport)) return;
        CopyClientState(previous, transport);
        ClearMessageState(previous);
        Client = transport;
        previous.Dispose();
    }

    private static void CopyClientState(ClientSmtp source, ClientSmtp target) {
        target.Subject = source.Subject;
        target.HtmlBody = source.HtmlBody;
        target.TextBody = source.TextBody;
        target.Attachments = source.Attachments;
        target.InlineAttachments = source.InlineAttachments;
        target.Headers = source.Headers;
        target.From = source.From;
        target.To = source.To;
        target.Cc = source.Cc;
        target.Bcc = source.Bcc;
        target.ReplyTo = source.ReplyTo;
        target.Message = source.Message;
        target.Priority = source.Priority;
        target.DeliveryNotificationOption = source.DeliveryNotificationOption;
        target.AutoEmbedRemoteImages = source.AutoEmbedRemoteImages;
        target.RemoteImageDownloadOptions = source.RemoteImageDownloadOptions;
        if (target.Timeout != source.Timeout) target.Timeout = source.Timeout;
        target.CheckCertificateRevocation = source.CheckCertificateRevocation;
        target.ServerCertificateValidationCallback = source.ServerCertificateValidationCallback;
        target.LocalDomain = source.LocalDomain;
        target.DeliveryStatusNotificationType = source.DeliveryStatusNotificationType;
    }

    private static void ClearMessageState(ClientSmtp client) {
        client.Subject = client.HtmlBody = client.TextBody = string.Empty;
        client.From = client.ReplyTo = null;
        client.To = client.Cc = client.Bcc = null;
        client.Attachments = client.InlineAttachments = null;
        client.Headers = null;
        client.Message = new MimeMessage();
        client.Priority = default;
        client.DeliveryNotificationOption = null;
        client.AutoEmbedRemoteImages = false;
        client.RemoteImageDownloadOptions = new RemoteImageDownloadOptions();
    }
}
