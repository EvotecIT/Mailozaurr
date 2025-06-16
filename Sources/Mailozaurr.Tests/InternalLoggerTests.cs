using Xunit;

namespace Mailozaurr.Tests;

public class InternalLoggerTests
{
    [Fact]
    public void WriteProgress_RaisesEventWithCorrectPercentage()
    {
        var logger = new Mailozaurr.InternalLogger();
        int? percentage = null;
        logger.OnProgressMessage += (_, e) => percentage = e.ProgressPercentage;

        logger.WriteProgress("Activity", "Operation", 42);

        Assert.Equal(42, percentage);
    }
}
