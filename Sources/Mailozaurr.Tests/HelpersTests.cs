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
    public void ConvertFromPlainText_ThrowsArgumentNullException_WhenUserNameIsNull() {
        Assert.Throws<ArgumentNullException>(() => Mailozaurr.Helpers.ConvertFromPlainText(null!, "secret"));
    }

    [Fact]
    public void ConvertFromPlainText_ThrowsArgumentNullException_WhenPasswordIsNull() {
        Assert.Throws<ArgumentNullException>(() => Mailozaurr.Helpers.ConvertFromPlainText("user", null!));
    }

    [Fact]
    public void ConvertFromPlainText_ThrowsArgumentException_WhenUserNameIsEmpty() {
        Assert.Throws<ArgumentException>(() => Mailozaurr.Helpers.ConvertFromPlainText(string.Empty, "secret"));
    }

    [Fact]
    public void ConvertFromPlainText_ThrowsArgumentException_WhenPasswordIsEmpty() {
        Assert.Throws<ArgumentException>(() => Mailozaurr.Helpers.ConvertFromPlainText("user", string.Empty));
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

    [Fact]
    public void UniqueAddresses_DefaultsToNewSet_WhenSeenIsNull() {
        var addresses = new object[] { "a@example.com", "a@example.com", "b@example.com" };

        var result = Mailozaurr.Helpers
            .UniqueAddresses(addresses, null)
            .Select(Mailozaurr.Helpers.GetEmailAddress)
            .ToArray();

        Assert.Equal(new[] { "a@example.com", "b@example.com" }, result);
    }

    [Fact]
    public void UniqueAddresses_UsesProvidedSetAcrossCalls() {
        var seen = new HashSet<string>();
        var first = new object[] { "a@example.com" };
        var second = new object[] { "a@example.com", "b@example.com" };

        Mailozaurr.Helpers
            .UniqueAddresses(first, seen)
            .ToArray();

        var result = Mailozaurr.Helpers
            .UniqueAddresses(second, seen)
            .Select(Mailozaurr.Helpers.GetEmailAddress)
            .ToArray();

        Assert.Equal(new[] { "b@example.com" }, result);
    }

    private class CancelHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }

    private class ThrowHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }

    private class FailStatusHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private class CountingHandler : HttpMessageHandler {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
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

    [Fact]
    public async Task PostWebhookAsync_NonSuccessStatus_LogsWarning() {
        var client = new HttpClient(new FailStatusHandler());
        var result = new SmtpResult(true, EmailAction.Send, string.Empty, string.Empty, string.Empty, 0, TimeSpan.Zero);
        var messages = new List<string>();
        void Handler(object? _, LogEventArgs e) => messages.Add(e.Message);
        Mailozaurr.LoggingMessages.Logger.OnWarningMessage += Handler;

        await Mailozaurr.Helpers.PostWebhookAsync("http://localhost", result, default, client);

        Mailozaurr.LoggingMessages.Logger.OnWarningMessage -= Handler;
        Assert.Contains(messages, static m => m.Contains("Failed to post webhook"));
    }

    [Fact]
    public async Task PostWebhookAsync_UsesSharedClient_WhenClientNotProvided() {
        var handler = new CountingHandler();
        var client = new HttpClient(handler);
        Mailozaurr.Helpers.SharedHttpClient = client;

        try {
            var result = new SmtpResult(true, EmailAction.Send, string.Empty, string.Empty, string.Empty, 0, TimeSpan.Zero);
            await Mailozaurr.Helpers.PostWebhookAsync("http://localhost", result);
            await Mailozaurr.Helpers.PostWebhookAsync("http://localhost", result);
            Assert.Equal(2, handler.Calls);
        } finally {
            Mailozaurr.Helpers.SharedHttpClient = new HttpClient();
        }
    }

    private class DisposeTrackingHandler : HttpMessageHandler {
        public bool Disposed;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Disposed = true;
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task SharedHttpClient_ReplacesAndDisposesPreviousClient() {
        var handler1 = new DisposeTrackingHandler();
        var client1 = new HttpClient(handler1);
        Mailozaurr.Helpers.SharedHttpClient = client1;

        var handler2 = new CountingHandler();
        var client2 = new HttpClient(handler2);
        Mailozaurr.Helpers.SharedHttpClient = client2;

        Assert.True(handler1.Disposed);

        using var response = await Mailozaurr.Helpers.SharedHttpClient.GetAsync("http://localhost");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, handler2.Calls);

        Mailozaurr.Helpers.SharedHttpClient = new HttpClient();
    }
}