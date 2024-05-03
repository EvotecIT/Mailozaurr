namespace Mailozaurr;

/// <summary>
/// Settings for the Mailozaurr library.
/// </summary>
public static class Settings {
    /// <summary>
    /// The logger instance.
    /// </summary>
    public static InternalLogger Logger = new InternalLogger();

    /// <summary>
    /// Gets or sets a value indicating whether error logging is enabled.
    /// </summary>
    public static bool Error {
        get => Logger.IsError;
        set => Logger.IsError = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether verbose logging is enabled.
    /// </summary>
    public static bool Verbose {
        get => Logger.IsVerbose;
        set => Logger.IsVerbose = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether warning logging is enabled.
    /// </summary>
    public static bool Warning {
        get => Logger.IsWarning;
        set => Logger.IsWarning = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether progress logging is enabled.
    /// </summary>
    public static bool Progress {
        get => Logger.IsProgress;
        set => Logger.IsProgress = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether debug logging is enabled.
    /// </summary>
    public static bool Debug {
        get => Logger.IsDebug;
        set => Logger.IsDebug = value;
    }

    /// <summary>
    /// The lock object
    /// </summary>
    static readonly object _LockObject = new object();
}
