using System;
using Xunit;

namespace Mailozaurr.Tests;

public class LoggingConfiguratorDisposeTests
{
    [Fact]
    public void Dispose_ReleasesResources()
    {
        var configurator = new LoggingConfigurator();
        configurator.ConfigureLogging(string.Empty, false, true, false, false);

        var stream = configurator.LogStream!;
        var logger = configurator.ProtocolLogger!;

        configurator.Dispose();

        Assert.Null(configurator.LogStream);
        Assert.Null(configurator.ProtocolLogger);
        Assert.Throws<ObjectDisposedException>(() => stream.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => logger.LogClient(new byte[] { 0x41 }, 0, 1));
    }
}
