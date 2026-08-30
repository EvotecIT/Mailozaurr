using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

/// <summary>
/// Creates Amazon SES requests whose signed headers exactly match the transmitted request.
/// </summary>
internal static class SesRequestFactory {
    internal const string FormContentType = "application/x-www-form-urlencoded";

    internal static HttpRequestMessage Create(
        string accessKey,
        string secretKey,
        string region,
        string content,
        DateTime utcNow) {
        SesRegionName.Validate(region, nameof(region));
        var amzDate = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var canonicalHeaders =
            $"content-type:{FormContentType}\n" +
            $"host:email.{region}.amazonaws.com\n" +
            $"x-amz-date:{amzDate}\n";
        const string signedHeaders = "content-type;host;x-amz-date";
        var payloadHash = Sha256Hex(content);
        var canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var credentialScope = $"{dateStamp}/{region}/ses/aws4_request";
        var stringToSign =
            $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";
        var signingKey = GetSignatureKey(secretKey, dateStamp, region, "ses");
        var signature = ToHex(HmacSha256(signingKey, stringToSign));
        var authorization =
            $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, " +
            $"SignedHeaders={signedHeaders}, Signature={signature}";

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"https://email.{region}.amazonaws.com/"));
        var requestContent = new StringContent(content, Encoding.UTF8);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue(FormContentType);
        request.Content = requestContent;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return request;
    }

    private static byte[] GetSignatureKey(
        string key,
        string dateStamp,
        string regionName,
        string serviceName) {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + key), dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data) {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Sha256Hex(string data) {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    private static string ToHex(byte[] bytes) {
        var builder = new StringBuilder(bytes.Length * 2);
        for (var index = 0; index < bytes.Length; index++) {
            builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }
}
