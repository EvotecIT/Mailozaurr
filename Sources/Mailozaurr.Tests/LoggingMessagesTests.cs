using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class LoggingMessagesTests
{
    [Fact]
    public async Task Logger_IsAsyncLocalPerTask()
    {
        var original = LoggingMessages.Logger;
        InternalLogger? task1Logger = null;
        InternalLogger? task2Logger = null;

        await Task.WhenAll(
            Task.Run(() =>
            {
                LoggingMessages.Logger = new InternalLogger();
                task1Logger = LoggingMessages.Logger;
            }),
            Task.Run(() =>
            {
                LoggingMessages.Logger = new InternalLogger();
                task2Logger = LoggingMessages.Logger;
            })
        );

        Assert.NotSame(task1Logger, task2Logger);
        Assert.Same(original, LoggingMessages.Logger);
    }
}
