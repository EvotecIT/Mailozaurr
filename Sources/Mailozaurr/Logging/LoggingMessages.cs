namespace Mailozaurr;

public class LoggingMessages {
    public static InternalLogger Logger = new InternalLogger();

    public static bool Error {
        get => Logger.IsError;
        set => Logger.IsError = value;
    }

    public static bool Verbose {
        get => Logger.IsVerbose;
        set => Logger.IsVerbose = value;
    }

    public static bool Warning {
        get => Logger.IsWarning;
        set => Logger.IsWarning = value;
    }

    public static bool Progress {
        get => Logger.IsProgress;
        set => Logger.IsProgress = value;
    }

    public static bool Debug {
        get => Logger.IsDebug;
        set => Logger.IsDebug = value;
    }

    internal static object _LockObject = new object();
}
