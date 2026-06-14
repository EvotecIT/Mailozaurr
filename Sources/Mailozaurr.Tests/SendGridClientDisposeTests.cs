namespace Mailozaurr.Tests;

public class SendGridClientDisposeTests {
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes() {
        using var client = new SendGridClient();
        client.Dispose();
        client.Dispose();
    }

    [Fact]
    public async Task SendEmailAsync_AfterDispose_ThrowsObjectDisposedException() {
        var client = new SendGridClient();
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendEmailAsync());
    }
}