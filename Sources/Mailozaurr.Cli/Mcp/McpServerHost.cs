using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mailozaurr.Application;

namespace Mailozaurr.Cli.Mcp;

internal static class McpServerHost {
    public static async Task RunAsync(MailApplication application, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(application);

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Services.AddSingleton(application);
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(MailMcpTools).Assembly);

        using var host = builder.Build();
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
