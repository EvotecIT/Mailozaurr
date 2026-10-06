#if NET8_0_OR_GREATER

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>
/// The Web Push protocol without a third-party library: payload encryption (RFC 8291, aes128gcm) so only the browser
/// can read a notification, and VAPID signing (RFC 8292) so push services know this installation sent it.
/// </summary>
public static class WebPush {
    /// <summary>Record size written in the header; one record holds the whole payload.</summary>
    private const int RecordSize = 4096;

    /// <summary>Largest payload encrypted; push services accept about 4 KB.</summary>
    public const int MaxPayloadBytes = 3_000;

    /// <summary>
    /// Push services a subscription may point at. The server sends requests to the address a browser gives, so only
    /// the browsers' own push services are accepted, never an arbitrary host.
    /// </summary>
    private static readonly string[] ServiceHosts = ["fcm.googleapis.com", "updates.push.services.mozilla.com", "web.push.apple.com"];
    private static readonly string[] ServiceSuffixes = [".notify.windows.com", ".push.apple.com", ".push.services.mozilla.com"];

    /// <summary>Whether an endpoint is an HTTPS address at a known browser push service.</summary>
    public static bool IsPushService(Uri endpoint) {
        ArgumentNullException.ThrowIfNull(endpoint);
        return endpoint.IsAbsoluteUri && endpoint.Fragment.Length == 0 && endpoint.Scheme == Uri.UriSchemeHttps && endpoint.IsDefaultPort && endpoint.UserInfo.Length == 0
            && (ServiceHosts.Contains(endpoint.Host, StringComparer.OrdinalIgnoreCase)
                || ServiceSuffixes.Any(suffix => endpoint.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Checks that a subscription key is a usable P-256 point before it can enter the delivery queue.</summary>
    public static bool IsBrowserKey(byte[] point) {
        if (point is not [0x04, ..] || point.Length != 65) return false;
        try {
            using ECDiffieHellman browser = ECDiffieHellman.Create(new ECParameters {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = point[1..33], Y = point[33..65] }
            });
            using ECDiffieHellman local = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            local.DeriveRawSecretAgreement(browser.PublicKey);
            return true;
        } catch (CryptographicException) {
            return false;
        } catch (ArgumentException) {
            return false;
        } catch (PlatformNotSupportedException) {
            return false;
        }
    }

    /// <summary>A new P-256 key pair: the public key as an uncompressed point and the private scalar, both base64url.</summary>
    public static (string PublicKey, string PrivateKey) NewKeyPair() {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = key.ExportParameters(includePrivateParameters: true);
        return (EncodeBase64Url(Point(parameters)), EncodeBase64Url(parameters.D!));
    }

    /// <summary>
    /// Encrypts a payload with a fresh key pair and salt using the browser's public key and authentication secret.
    /// </summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] browserPublicKey, byte[] authSecret) =>
        Encrypt(plaintext, browserPublicKey, authSecret, null, null);

    // Deterministic inputs are internal for the published RFC test vector.
    internal static byte[] Encrypt(byte[] plaintext, byte[] browserPublicKey, byte[] authSecret, ECDiffieHellman? serverKey, byte[]? salt) {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(browserPublicKey);
        ArgumentNullException.ThrowIfNull(authSecret);
        if (plaintext.Length > MaxPayloadBytes) throw new ArgumentException("The payload is too large for a push message.", nameof(plaintext));
        if (browserPublicKey is not [0x04, ..] || browserPublicKey.Length != 65) throw new ArgumentException("The browser key is not an uncompressed P-256 point.", nameof(browserPublicKey));
        if (authSecret.Length != 16) throw new ArgumentException("The auth secret must be 16 bytes.", nameof(authSecret));

        using ECDiffieHellman ephemeral = serverKey is null ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256) : null!;
        ECDiffieHellman local = serverKey ?? ephemeral;
        byte[] salted = salt ?? RandomNumberGenerator.GetBytes(16);
        if (salted.Length != 16) throw new ArgumentException("The salt must contain 16 bytes.", nameof(salt));
        byte[] serverPublic = Point(local.ExportParameters(includePrivateParameters: false));

        using ECDiffieHellman browser = ECDiffieHellman.Create(new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = browserPublicKey[1..33], Y = browserPublicKey[33..65] }
        });
        byte[] shared = local.DeriveRawSecretAgreement(browser.PublicKey);

        // key_info = "WebPush: info" || 0x00 || ua_public || as_public
        byte[] keyInfo = [.. "WebPush: info\0"u8, .. browserPublicKey, .. serverPublic];
        byte[] ikm = HKDF.Expand(HashAlgorithmName.SHA256, HKDF.Extract(HashAlgorithmName.SHA256, shared, authSecret), 32, keyInfo);
        byte[] prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salted);
        byte[] cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, "Content-Encoding: aes128gcm\0"u8.ToArray());
        byte[] nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, "Content-Encoding: nonce\0"u8.ToArray());

        // One record: the payload and the delimiter that marks the last record.
        byte[] padded = [.. plaintext, 0x02];
        byte[] cipher = new byte[padded.Length];
        byte[] tag = new byte[16];
        using (AesGcm aes = new(cek, tag.Length)) aes.Encrypt(nonce, padded, cipher, tag);

        // Header: salt (16) || record size (4, big-endian) || key id length (1) || key id (the server's public key).
        byte[] header = new byte[16 + 4 + 1];
        salted.CopyTo(header, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16, 4), RecordSize);
        header[20] = (byte)serverPublic.Length;
        return [.. header, .. serverPublic, .. cipher, .. tag];
    }

    /// <summary>
    /// The Authorization header value for one push service: a short-lived ES256 token naming the service and a way to
    /// reach the operator, and the installation's public key.
    /// </summary>
    public static string Authorization(Uri endpoint, string subject, string publicKey, string privateKey, DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (!IsPushService(endpoint)) throw new ArgumentException("Expected a browser push-service endpoint.", nameof(endpoint));
        if (!Uri.TryCreate(subject, UriKind.Absolute, out var contact) || (contact.Scheme != "mailto" && contact.Scheme != "https")) throw new ArgumentException("The VAPID subject must be a mailto or HTTPS contact URI.", nameof(subject));
        byte[] point = DecodeBase64Url(publicKey);
        if (!IsBrowserKey(point)) throw new ArgumentException("Invalid application public key.", nameof(publicKey));
        using ECDsa key = ECDsa.Create(new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256,
            D = DecodeBase64Url(privateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..65] }
        });
        string header = EncodeBase64Url("{\"typ\":\"JWT\",\"alg\":\"ES256\"}"u8);
        using var claimsBuffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(claimsBuffer)) {
            writer.WriteStartObject();
            writer.WriteString("aud",endpoint.GetLeftPart(UriPartial.Authority));
            writer.WriteNumber("exp",now.AddHours(12).ToUnixTimeSeconds());
            writer.WriteString("sub",subject);
            writer.WriteEndObject();
        }
        string claims = EncodeBase64Url(claimsBuffer.ToArray());
        // ECDsa signs in the IEEE P1363 form (r || s) that JWS expects.
        byte[] signature = key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256);
        return $"vapid t={header}.{claims}.{EncodeBase64Url(signature)}, k={publicKey}";
    }

    internal static string EncodeBase64Url(ReadOnlySpan<byte> value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');
    internal static byte[] DecodeBase64Url(string value) {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 512 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) throw new FormatException("Invalid base64url key.");
        return Convert.FromBase64String(value.Replace('-','+').Replace('_','/').PadRight((value.Length + 3) / 4 * 4, '='));
    }

    private static byte[] Point(ECParameters parameters) => [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
}

#endif
