using System;
using System.Threading.Tasks;

/// <summary>
/// Demonstrates using <see cref="Mailozaurr.GraphMessageListener"/> with delta queries for incremental updates.
/// </summary>
public static class GraphMessageListenerDeltaExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        var credential = new Mailozaurr.GraphCredential {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            DirectoryId = "tenant-id"
        };
        const string user = "user@example.com";

        using var listener = new Mailozaurr.GraphMessageListener(credential, user, TimeSpan.FromMinutes(1));
        listener.MessageArrived += (s, msg) =>
            Console.WriteLine("New or updated message: " + msg["id"]);

        await listener.StartAsync();

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        listener.Stop();
    }
}
