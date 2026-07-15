#if NET8_0_OR_GREATER
using System.Reflection;
using Mailozaurr.Cli.Mcp;
using ModelContextProtocol.Server;

namespace Mailozaurr.Tests;

public sealed class MailMcpToolsMetadataTests {
    private static readonly HashSet<string> RawSecretParameterNames = new(StringComparer.OrdinalIgnoreCase) {
        "accessToken",
        "certificatePassword",
        "clientSecret",
        "refreshToken",
        "secretValue"
    };

    [Fact]
    public void ReadToolsDeclareSafeMcpMetadata() {
        var attribute = GetToolAttribute(nameof(MailMcpTools.mail_search));

        Assert.True(attribute.ReadOnly);
        Assert.False(attribute.Destructive);
        Assert.True(attribute.Idempotent);
    }

    [Fact]
    public void MutatingToolsRetainConservativeMcpMetadata() {
        var attribute = GetToolAttribute(nameof(MailMcpTools.mail_delete));

        Assert.False(attribute.ReadOnly);
        Assert.True(attribute.Destructive);
    }

    [Fact]
    public void McpToolsDoNotAcceptRawSecretValues() {
        var unsafeParameters = typeof(MailMcpTools)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.Name is not null && RawSecretParameterNames.Contains(parameter.Name))
                .Select(parameter => $"{method.Name}.{parameter.Name}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unsafeParameters);
    }

    private static McpServerToolAttribute GetToolAttribute(string methodName) {
        var method = typeof(MailMcpTools).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var attribute = method.GetCustomAttribute<McpServerToolAttribute>();
        Assert.NotNull(attribute);
        return attribute;
    }
}
#endif
