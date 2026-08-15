using System.Net;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphApiErrorParserTests {
    [Fact]
    public void Parse_ShouldExtractInformation() {
        var sample = "POST https://graph.microsoft.com/v1.0/users/przemyslaw.klys@company.pl/sendMail\n" +
                     "HTTP/2.0 404 Not Found\n" +
                     "request-id: 2ff18766-1395-4fb9-abd1-162774d4b063\n" +
                     "client-request-id: 6c57f9e6-3cad-48ee-8f7a-d566dc92aca3\n" +
                     "x-ms-ags-diagnostic: {\"ServerInfo\":{\"DataCenter\":\"Poland Central\",\"Slice\":\"E\",\"Ring\":\"2\",\"ScaleUnit\":\"002\",\"RoleInstance\":\"WA3PEPF000004A2\"}}\n" +
                     "Date: Sun, 24 Aug 2025 12:55:18 GMT\n" +
                     "Content-Type: application/json; odata.metadata=minimal; odata.streaming=true; IEEE754Compatible=false; charset=utf-8\n\n" +
                     "{\"error\":{\"code\":\"ErrorInvalidUser\",\"message\":\"The requested user 'przemyslaw.klys@company.pl' is invalid.\"}}";

        var parsed = GraphApiErrorParser.Parse(sample);
        Assert.NotNull(parsed);
        Assert.Equal(HttpStatusCode.NotFound, parsed!.StatusCode);
        Assert.Equal(GraphHttpMethod.POST, parsed.Method);
        Assert.Equal("2ff18766-1395-4fb9-abd1-162774d4b063", parsed.Headers.RequestId);
        Assert.Equal("Poland Central", parsed.Headers.Diagnostic?.ServerInfo.DataCenter);
        Assert.Equal("ErrorInvalidUser", parsed.Error?.Code);
    }

    [Fact]
    public void Parse_InvalidInput_ReturnsRaw() {
        const string sample = "not a graph error";
        var parsed = GraphApiErrorParser.Parse(sample);
        Assert.NotNull(parsed);
        Assert.Equal(sample, parsed!.Raw);
        Assert.Null(parsed.Error);
    }

    [Fact]
    public void Parse_StandaloneJson_UsesSuppliedStatusCode() {
        const string sample = "{\"error\":{\"code\":\"ErrorInvalidUser\",\"message\":\"Invalid mailbox\"}}";

        var parsed = GraphApiErrorParser.Parse(sample, HttpStatusCode.BadRequest);

        Assert.NotNull(parsed);
        Assert.Equal(HttpStatusCode.BadRequest, parsed!.StatusCode);
        Assert.Equal("ErrorInvalidUser", parsed.Error?.Code);
        Assert.Equal("Invalid mailbox", parsed.Error?.Message);
    }

    [Fact]
    public void Parse_MessageContainingJson_ExtractsStructuredError() {
        const string sample = "Unknown error: {\"error\":{\"code\":\"ErrorAccessDenied\",\"message\":\"Denied\"}}";

        var parsed = GraphApiErrorParser.Parse(sample);

        Assert.NotNull(parsed);
        Assert.Equal("ErrorAccessDenied", parsed!.Error?.Code);
    }
}
