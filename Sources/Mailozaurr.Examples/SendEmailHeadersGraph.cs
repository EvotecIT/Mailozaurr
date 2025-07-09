using Mailozaurr;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

public static class SendEmailHeadersGraph
{
    public static async Task RunAsync()
    {
        using var graph = new Graph();
        graph.From = "sender@example.com";
        graph.To = new[] { "recipient@example.com" };
        graph.Subject = "Graph Headers";
        graph.HTML = "<p>Hello</p>";
        graph.Headers = new Dictionary<string, string>
        {
            ["X-Tracking-ID"] = "abc123",
            ["X-Source"] = "Mailozaurr"
        };
        var cred = new NetworkCredential("clientid@tenant", "secret");
        graph.Authenticate(cred);
        var connect = await graph.ConnectO365GraphAsync();
        if (connect.Status)
        {
            await graph.SendMessageAsync();
        }
    }
}
