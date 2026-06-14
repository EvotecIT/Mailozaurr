using System;

/// <summary>
/// Example demonstrating usage of <see cref="GraphEndpoint"/> with MicrosoftGraphUtils.
/// </summary>
public static class BuildGraphUriExample {
    public static void Run() {
        string uri = Mailozaurr.MicrosoftGraphUtils.BuildGraphUri(Mailozaurr.GraphEndpoint.V1, "/users/me/messages");
        Console.WriteLine(uri);
    }
}