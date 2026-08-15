using Mailozaurr.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mailozaurr.Cli.Mcp;

[McpServerToolType]
public sealed partial class MailMcpTools {
    private readonly MailApplication _application;

    public MailMcpTools(MailApplication application) {
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }




}