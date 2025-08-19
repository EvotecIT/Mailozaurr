using System;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Enables routing of <see cref="InternalLogger"/> events to PowerShell streams.
/// </summary>
public class InternalLoggerPowerShell : IDisposable {
    private readonly InternalLogger _logger;
    private readonly Action<string>? _writeVerboseAction;
    private readonly Action<string>? _writeDebugAction;
    private readonly Action<InformationRecord>? _writeInformationAction;
    private readonly Action<string>? _writeWarningAction;
    private readonly Action<ErrorRecord>? _writeErrorAction;
    private readonly Action<ProgressRecord>? _writeProgressAction;

    /// <summary>
    /// Creates a new instance of the <see cref="InternalLoggerPowerShell"/> class.
    /// </summary>
    /// <param name="logger">Source logger that exposes events.</param>
    /// <param name="writeVerboseAction">Delegate used to write verbose messages.</param>
    /// <param name="writeWarningAction">Delegate used to write warning messages.</param>
    /// <param name="writeDebugAction">Delegate used to write debug messages.</param>
    /// <param name="writeErrorAction">Delegate used to write error records.</param>
    /// <param name="writeProgressAction">Delegate used to write progress records.</param>
    /// <param name="writeInformationAction">Delegate used to write information records.</param>
    public InternalLoggerPowerShell(InternalLogger logger, Action<string>? writeVerboseAction = null, Action<string>? writeWarningAction = null, Action<string>? writeDebugAction = null, Action<ErrorRecord>? writeErrorAction = null, Action<ProgressRecord>? writeProgressAction = null, Action<InformationRecord>? writeInformationAction = null) {
        _logger = logger;

        if (writeVerboseAction != null) {
            _writeVerboseAction = writeVerboseAction;
            _logger.OnVerboseMessage += Logger_OnVerboseMessage;
        }

        if (writeWarningAction != null) {
            _writeWarningAction = writeWarningAction;
            _logger.OnWarningMessage += Logger_OnWarningMessage;
        }

        if (writeDebugAction != null) {
            _writeDebugAction = writeDebugAction;
            _logger.OnDebugMessage += Logger_OnDebugMessage;
        }

        if (writeErrorAction != null) {
            _writeErrorAction = writeErrorAction;
            _logger.OnErrorMessage += Logger_OnErrorMessage;
        }

        if (writeProgressAction != null) {
            _writeProgressAction = writeProgressAction;
            _logger.OnProgressMessage += Logger_OnProgressMessage;
        }

        if (writeInformationAction != null) {
            _writeInformationAction = writeInformationAction;
            _logger.OnInformationMessage += Logger_OnInformationMessage;
        }
    }

    /// <summary>
    /// Handles verbose messages from the logger.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event data.</param>
    private void Logger_OnVerboseMessage(object? sender, LogEventArgs e) {
        if (e.Args != null && e.Args.Length > 0) {
            WriteVerbose(e.Message, e.Args);
        } else {
            WriteVerbose(e.Message);
        }
    }

    private void Logger_OnDebugMessage(object? sender, LogEventArgs e) {
        WriteDebug(e.Message);
    }

    private void Logger_OnWarningMessage(object? sender, LogEventArgs e) {
        WriteWarning(e.Message);
    }

    private void Logger_OnErrorMessage(object? sender, LogEventArgs e) {
        ErrorRecord errorRecord = new ErrorRecord(new Exception(e.Message), "1", ErrorCategory.NotSpecified, null);
        WriteError(errorRecord);
    }

    private int _activityIdCounter = 0;
    private int _currentActivityId = 1;
    private bool _isCurrentActivityCompleted = true;

    private int GetNextActivityId() {
        return ++_activityIdCounter;
    }

    private void Logger_OnProgressMessage(object? sender, LogEventArgs e) {
        if (_isCurrentActivityCompleted) {
            _currentActivityId = GetNextActivityId();
            _isCurrentActivityCompleted = false;
        }
        var progressMessage = e.ProgressCurrentOperation ?? "Processing...: ";
        var progressRecord = new ProgressRecord(_currentActivityId, e.ProgressActivity, progressMessage);
        if (e.ProgressPercentage.HasValue) {
            if (e.ProgressPercentage.Value >= 0 && e.ProgressPercentage.Value <= 100) {
                progressRecord.PercentComplete = e.ProgressPercentage.Value;
            } else {
                progressRecord.PercentComplete = 100;
            }
        } else {
            progressRecord.PercentComplete = 50;
        }
        if (progressRecord.PercentComplete == 100) {
            progressRecord.RecordType = ProgressRecordType.Completed;
            _isCurrentActivityCompleted = true;
        }
        WriteProgress(progressRecord);
    }

    private void Logger_OnInformationMessage(object? sender, LogEventArgs e) {
        WriteInformation(e.Message);
    }

    private void WriteVerbose(string message) {
        _writeVerboseAction?.Invoke(message);
    }

    /// <summary>
    /// Writes a formatted verbose message.
    /// </summary>
    /// <param name="message">Message template.</param>
    /// <param name="eArgs">Arguments for the template.</param>
    private void WriteVerbose(string message, object[] eArgs) {
        var fullMessage = string.Format(message, eArgs);
        _writeVerboseAction?.Invoke(fullMessage);
    }

    private void WriteDebug(string message) {
        _writeDebugAction?.Invoke(message);
    }

    private void WriteInformation(string message) {
        InformationRecord informationRecord = new InformationRecord(message, "Mailozaurr");
        _writeInformationAction?.Invoke(informationRecord);
    }

    private void WriteWarning(string message) {
        _writeWarningAction?.Invoke(message);
    }

    private void WriteError(ErrorRecord errorRecord) {
        _writeErrorAction?.Invoke(errorRecord);
    }

    private void WriteProgress(ProgressRecord progressRecord) {
        _writeProgressAction?.Invoke(progressRecord);
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_writeVerboseAction != null) {
            _logger.OnVerboseMessage -= Logger_OnVerboseMessage;
        }
        if (_writeWarningAction != null) {
            _logger.OnWarningMessage -= Logger_OnWarningMessage;
        }
        if (_writeDebugAction != null) {
            _logger.OnDebugMessage -= Logger_OnDebugMessage;
        }
        if (_writeErrorAction != null) {
            _logger.OnErrorMessage -= Logger_OnErrorMessage;
        }
        if (_writeProgressAction != null) {
            _logger.OnProgressMessage -= Logger_OnProgressMessage;
        }
        if (_writeInformationAction != null) {
            _logger.OnInformationMessage -= Logger_OnInformationMessage;
        }
        GC.SuppressFinalize(this);
    }
}

