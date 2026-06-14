using System;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphUploadSessionParsingTests {
    [Fact]
    public void ParseUploadSessionResult_WithoutUploadUrl_Throws() {
        MethodInfo? method = typeof(Graph).GetMethod(
            "ParseUploadSessionResult",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var ex = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, new object[] { "{}" }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Upload URL not found", ex.InnerException!.Message, StringComparison.Ordinal);
    }
}