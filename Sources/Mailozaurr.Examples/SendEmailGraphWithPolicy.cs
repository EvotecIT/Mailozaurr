using System;
using System.Net;
using System.Threading.Tasks;

namespace Mailozaurr.Examples;

public static class SendEmailGraphWithPolicy {
    public static async Task RunAsync() {
        var policy = new GraphSendPolicy {
            MaxConcurrency = 2,
            MaxRetries = 4,
            BaseDelayMs = 1000,
            MaxDelayMs = 30000,
            JitterMs = 500,
            RetryOnTransient = true,
            EnableSmtpFallback = true
        };

        using var graph = new Graph()
            .WithSendPolicy(policy)
            .WithSmtpFallback(() => {
                var smtp = new Smtp();
                smtp.Connect("smtp.office365.com", 587);
                smtp.Authenticate(new NetworkCredential("user@example.com", "password"));
                return smtp;
            });

        graph.From = "sender@example.com";
        graph.To = new object[] { "recipient@example.com" };
        graph.Subject = "Graph policy demo";
        graph.HTML = "<b>Hello</b>";
        graph.Authenticate(new NetworkCredential("clientid@tenant.onmicrosoft.com", "client-secret"));

        var connect = await graph.ConnectO365GraphAsync();
        if (!connect.Status) {
            Console.WriteLine($"Connect failed: {connect.Error}");
            return;
        }
        var send = await graph.SendMessageAsync();
        Console.WriteLine($"Status: {send.Status}, Message: {send.Message ?? send.Error}");
    }
}