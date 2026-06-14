using System;
using System.IO;
using Xunit;

namespace Mailozaurr.Tests;

public class LoggingConfiguratorValidationTests {
    [Fact]
    public void ConfigureLogging_StripsNewLinesInPrefixes_AndCreatesDirectory() {
        var warnings = new System.Collections.Generic.List<string>();
        var logger = new InternalLogger();
        logger.OnWarningMessage += (_, e) => warnings.Add(e.FullMessage);
        LoggingMessages.Logger = logger;

        var tempDir = Path.Combine(Path.GetTempPath(), "mlz-logs", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(tempDir, "log.txt");
        var configurator = new LoggingConfigurator();
        try {
            configurator.ConfigureLogging(path, logConsole: false, logObject: false, logTimestamps: true, logSecrets: false, logTimestampsFormat: "O", logServerPrefix: "srv\n\r", logClientPrefix: "cli\n");

            Assert.True(File.Exists(path));
            Assert.Contains(warnings, m => m.IndexOf("prefix contains new lines", StringComparison.OrdinalIgnoreCase) >= 0);
        } finally {
            configurator.ProtocolLogger?.Dispose();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}