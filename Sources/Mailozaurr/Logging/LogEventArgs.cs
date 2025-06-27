namespace Mailozaurr;

/// <summary>
/// Represents the arguments for a log event.
/// </summary>
public class LogEventArgs : EventArgs {
    /// <summary>Progress percentage.</summary>
    public int? ProgressPercentage { get; set; }

    /// <summary>Progress total steps.</summary>
    public int? ProgressTotalSteps { get; set; }

    /// <summary>Progress current steps.</summary>
    public int? ProgressCurrentSteps { get; set; }

    /// <summary>Progress current operation.</summary>
    public string ProgressCurrentOperation { get; set; }

    /// <summary>Progress activity.</summary>
    public string ProgressActivity { get; set; }

    /// <summary>Message to be written including arguments substitution.</summary>
    public string FullMessage { get; set; }

    /// <summary>Message to be written.</summary>
    public string Message { get; set; }

    /// <summary>Gets or sets the arguments.</summary>
    public object[] Args { get; set; }

    /// <summary>Initializes a new instance of the <see cref="LogEventArgs"/> class.</summary>
    public LogEventArgs(string message, object[] args) {
        Message = message;
        Args = args;
        FullMessage = string.Format(message, args);
    }

    /// <summary>Initializes a new instance of the <see cref="LogEventArgs"/> class.</summary>
    public LogEventArgs(string message) {
        Message = message;
        FullMessage = message;
    }

    /// <summary>Initializes a new instance of the <see cref="LogEventArgs"/> class.</summary>
    public LogEventArgs(string activity, string currentOperation, int? currentSteps, int? totalSteps, int? percentage) {
        ProgressActivity = activity;
        ProgressCurrentOperation = currentOperation;
        ProgressCurrentSteps = currentSteps;
        ProgressTotalSteps = totalSteps;
        ProgressPercentage = percentage;
    }
}
