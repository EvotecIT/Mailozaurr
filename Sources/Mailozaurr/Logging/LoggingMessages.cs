namespace Mailozaurr;

/// <summary>
/// Static accessors for the global <see cref="InternalLogger"/> instance used throughout the library.
/// </summary>
/// <remarks>
/// The logger can be enabled or disabled via these properties to
/// control how much diagnostic information is emitted.
/// </remarks>
public class LoggingMessages {
    /// <summary>Gets the global logger.</summary>
    public static InternalLogger Logger = new InternalLogger();

    /// <summary>Enable or disable error logging.</summary>
    public static bool Error {
        get => Logger.IsError;
        set => Logger.IsError = value;
    }

    /// <summary>Enable or disable verbose logging.</summary>
    public static bool Verbose {
        get => Logger.IsVerbose;
        set => Logger.IsVerbose = value;
    }

    /// <summary>Enable or disable warning logging.</summary>
    public static bool Warning {
        get => Logger.IsWarning;
        set => Logger.IsWarning = value;
    }

    /// <summary>Enable or disable progress logging.</summary>
    public static bool Progress {
        get => Logger.IsProgress;
        set => Logger.IsProgress = value;
    }

    /// <summary>Enable or disable debug logging.</summary>
    public static bool Debug {
        get => Logger.IsDebug;
        set => Logger.IsDebug = value;
    }

    /// <summary>
    /// Internal lock object used to synchronize logging operations.
    /// </summary>
    internal static object _LockObject = new object();
}
