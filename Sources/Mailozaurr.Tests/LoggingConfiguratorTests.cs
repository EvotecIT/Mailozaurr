using System.IO;
using Xunit;

namespace Mailozaurr.Tests;

public class LoggingConfiguratorTests
{
    [Fact]
    public void ConfigureLogging_NullPath_UsesMemoryStream()
    {
        var configurator = new LoggingConfigurator();
        configurator.ConfigureLogging(null, false, true, false, false);

        Assert.Null(configurator.LogPath);
        Assert.NotNull(configurator.LogStream);
        Assert.NotNull(configurator.ProtocolLogger);
    }

    [Fact]
    public void ConfigureLogging_EmptyPath_UsesMemoryStream()
    {
        var configurator = new LoggingConfigurator();
        configurator.ConfigureLogging(string.Empty, false, true, false, false);

        Assert.Equal(string.Empty, configurator.LogPath);
        Assert.NotNull(configurator.LogStream);
        Assert.NotNull(configurator.ProtocolLogger);
    }

    [Fact]
    public void ConfigureLogging_ValidPath_CreatesFileLogger()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var configurator = new LoggingConfigurator();
        try
        {
            configurator.ConfigureLogging(path, false, false, false, false);

            Assert.Equal(path, configurator.LogPath);
            Assert.Null(configurator.LogStream);
            Assert.NotNull(configurator.ProtocolLogger);
            Assert.True(File.Exists(path));
        }
        finally
        {
            configurator.ProtocolLogger?.Dispose();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}