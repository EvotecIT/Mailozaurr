using Mailozaurr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

/// <summary>
/// Tests for miscellaneous helper methods.
/// </summary>
public class HelpersTests {
    [Fact]
    public void GetEmailAddress_ReturnsInputString_WhenStringProvided() {
        var result = Mailozaurr.Helpers.GetEmailAddress("test@example.com");
        Assert.Equal("test@example.com", result);
    }

    [Fact]
    public void GetEmailAddress_ReturnsEmailFromDictionary_WhenEmailKeyPresent() {
        var dict = new Dictionary<string, object> { { "Email", "dict@example.com" } };
        var result = Mailozaurr.Helpers.GetEmailAddress(dict);
        Assert.Equal("dict@example.com", result);
    }

    [Fact]
    public void GetEmailAddress_ReturnsEmpty_WhenEmailKeyMissing() {
        var dict = new Dictionary<string, object> { { "Name", "John" } };
        var result = Mailozaurr.Helpers.GetEmailAddress(dict);
        Assert.Equal(string.Empty, result);
    }

    private class CustomObject {
        public override string ToString() => "Custom";
    }

    [Fact]
    public void GetEmailAddress_ReturnsObjectToString_WhenNotStringOrDictionary() {
        var obj = new CustomObject();
        var result = Mailozaurr.Helpers.GetEmailAddress(obj);
        Assert.Equal("Custom", result);
    }

    [Fact]
    public void ConvertFromOAuth2Credential_ThrowsArgumentNullException_WhenCredentialIsNull() {
        Assert.Throws<ArgumentNullException>(() => Mailozaurr.Helpers.ConvertFromOAuth2Credential(null!));
    }

    [Fact]
    public void ConvertFromPlainText_ReturnsNetworkCredentialWithSecurePassword() {
        var cred = Mailozaurr.Helpers.ConvertFromPlainText("user", "secret");

        Assert.Equal("user", cred.UserName);
        Assert.Equal(6, cred.SecurePassword.Length);
        Assert.Equal("secret", cred.Password);
    }

    [Fact]
    public void CredentialToApiKey_ReturnsPassword_WhenNetworkCredential() {
        var cred = new NetworkCredential("apikey", "theKey");

        string result = Mailozaurr.Helpers.CredentialToApiKey(cred);

        Assert.Equal("theKey", result);
    }

    private class DummyCredentials : ICredentials {
        public NetworkCredential GetCredential(Uri uri, string authType) => new NetworkCredential();
    }

    [Fact]
    public void CredentialToApiKey_ThrowsArgumentException_WhenNotNetworkCredential() {
        ICredentials creds = new DummyCredentials();

        Assert.Throws<ArgumentException>(() => Mailozaurr.Helpers.CredentialToApiKey(creds));
    }

    [Fact]
    public void GetEmailAndName_ReturnsTuple_WhenDictionaryProvided() {
        var input = new Dictionary<string, object> { { "Email", "a@b.com" }, { "Name", "Alice" } };

        var (email, name) = Mailozaurr.Helpers.GetEmailAndName(input);

        Assert.Equal("a@b.com", email);
        Assert.Equal("Alice", name);
    }

    [Fact]
    public void GetEmailAndName_ReturnsEmailAndNullName_WhenStringProvided() {
        var (email, name) = Mailozaurr.Helpers.GetEmailAndName("c@d.com");

        Assert.Equal("c@d.com", email);
        Assert.Null(name);
    }

    [Fact]
    public void UniqueAddresses_RemovesDuplicates_IgnoringCaseAndWhitespace() {
        var addresses = new object[]
        {
            " Test@example.com ",
            "test@example.com",
            "other@example.com",
            "Other@example.com "
        };
        var seen = new HashSet<string>();
        var result = Mailozaurr.Helpers
            .UniqueAddresses(addresses!, seen)
            .Select(Mailozaurr.Helpers.GetEmailAddress)
            .ToArray();

        Assert.Equal(new[] { " Test@example.com ", "other@example.com" }, result);
    }

    [Fact]
    public void UniqueAddresses_SkipsNullOrInvalidEmails() {
        var addresses = new object?[]
        {
            null,
            new Dictionary<string, object> { { "Name", "Missing" } },
            " ",
            "valid@example.com"
        };
        var seen = new HashSet<string>();

        var result = Mailozaurr.Helpers
            .UniqueAddresses(addresses!, seen)
            .Select(Mailozaurr.Helpers.GetEmailAddress)
            .ToArray();

        Assert.Equal(new[] { "valid@example.com" }, result);
    }

    [Fact]
    public void UniqueAddresses_ReturnsFirstOccurrence_WhenDictionaryEmailsDuplicate() {
        var first = new Dictionary<string, object> { { "Email", "d@e.com" }, { "Name", "One" } };
        var second = new Dictionary<string, object> { { "Email", "D@e.com" }, { "Name", "Two" } };
        var addresses = new object[] { first, second };
        var seen = new HashSet<string>();

        var result = Mailozaurr.Helpers
            .UniqueAddresses(addresses, seen)
            .ToArray();

        Assert.Single(result);
        Assert.Same(first, result[0]);
    }

    private class CancelHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }

    private class ThrowHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }

    [Fact]
    public async Task PostWebhookAsync_CancellationRequested_ThrowsAsync() {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = new HttpClient(new CancelHandler());
        var result = new SmtpResult(true, EmailAction.Send, string.Empty, string.Empty, string.Empty, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            Mailozaurr.Helpers.PostWebhookAsync("http://localhost", result, cts.Token, client));
    }

    [Fact]
    public async Task PostWebhookAsync_HttpRequestException_LogsWarning() {
        var client = new HttpClient(new ThrowHandler());
        var result = new SmtpResult(true, EmailAction.Send, string.Empty, string.Empty, string.Empty, 0, TimeSpan.Zero);
        var messages = new List<string>();
        void Handler(object? _, LogEventArgs e) => messages.Add(e.Message);
        Mailozaurr.LoggingMessages.Logger.OnWarningMessage += Handler;

        await Mailozaurr.Helpers.PostWebhookAsync("http://localhost", result, default, client);

        Mailozaurr.LoggingMessages.Logger.OnWarningMessage -= Handler;
        Assert.Contains(messages, static m => m.Contains("Failed to post webhook"));
    }
}