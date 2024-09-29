using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// An abstract base class for asynchronous PowerShell cmdlets.
/// </summary>
public abstract class AsyncPSCmdlet : PSCmdlet, IDisposable {
    /// <summary>
    /// Defines the types of pipelines used in the cmdlet.
    /// </summary>
    private enum PipelineType {
        Output,
        OutputEnumerate,
        Error,
        Warning,
        Verbose,
        Debug,
        Information,
        Progress,
        ShouldProcess,
    }

    /// <summary>
    /// Cancels the processing of the cmdlet.
    /// </summary>
    private CancellationTokenSource _cancelSource = new();

    private BlockingCollection<(object?, PipelineType)>? _currentOutPipe;
    private BlockingCollection<object?>? _currentReplyPipe;

    /// <summary>
    /// Gets the cancellation token that is triggered when the cmdlet is stopped.
    /// </summary>
    protected internal CancellationToken CancelToken { get => _cancelSource.Token; }

    /// <summary>
    /// Begins processing the cmdlet asynchronously.
    /// </summary>
    protected override void BeginProcessing()
        => RunBlockInAsync(BeginProcessingAsync);

    /// <summary>
    /// Override this method to implement asynchronous begin processing logic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task BeginProcessingAsync()
        => Task.CompletedTask;

    /// <summary>
    /// Processes a record asynchronously.
    /// </summary>
    protected override void ProcessRecord()
        => RunBlockInAsync(ProcessRecordAsync);

    /// <summary>
    /// Override this method to implement asynchronous record processing logic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task ProcessRecordAsync()
        => Task.CompletedTask;

    /// <summary>
    /// Ends processing the cmdlet asynchronously.
    /// </summary>
    protected override void EndProcessing()
        => RunBlockInAsync(EndProcessingAsync);

    /// <summary>
    /// Override this method to implement asynchronous end processing logic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task EndProcessingAsync()
        => Task.CompletedTask;

    /// <summary>
    /// Stops the processing of the cmdlet.
    /// </summary>
    protected override void StopProcessing()
        => _cancelSource?.Cancel();

    /// <summary>
    /// Runs the specified task asynchronously and handles the output and reply pipelines.
    /// </summary>
    /// <param name="task">The task to run asynchronously.</param>
    private void RunBlockInAsync(Func<Task> task) {
        using BlockingCollection<(object?, PipelineType)> outPipe = new();
        using BlockingCollection<object?> replyPipe = new();
        Task blockTask = Task.Run(async () => {
            try {
                _currentOutPipe = outPipe;
                _currentReplyPipe = replyPipe;
                await task();
            } finally {
                _currentOutPipe = null;
                _currentReplyPipe = null;
                outPipe.CompleteAdding();
                replyPipe.CompleteAdding();
            }
        });

        foreach ((object? data, PipelineType pipelineType) in outPipe.GetConsumingEnumerable()) {
            switch (pipelineType) {
                case PipelineType.Output:
                    base.WriteObject(data);
                    break;

                case PipelineType.OutputEnumerate:
                    base.WriteObject(data, true);
                    break;

                case PipelineType.Error:
                    base.WriteError((ErrorRecord)data!);
                    break;

                case PipelineType.Warning:
                    base.WriteWarning((string)data!);
                    break;

                case PipelineType.Verbose:
                    base.WriteVerbose((string)data!);
                    break;

                case PipelineType.Debug:
                    base.WriteDebug((string)data!);
                    break;

                case PipelineType.Information:
                    base.WriteInformation((InformationRecord)data!);
                    break;

                case PipelineType.Progress:
                    base.WriteProgress((ProgressRecord)data!);
                    break;

                case PipelineType.ShouldProcess:
                    (string target, string action) = (ValueTuple<string, string>)data!;
                    bool res = base.ShouldProcess(target, action);
                    replyPipe.Add(res);
                    break;
            }
        }

        blockTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Determines whether the cmdlet should continue processing.
    /// </summary>
    /// <param name="target">The target of the operation.</param>
    /// <param name="action">The action to be performed.</param>
    /// <returns>True if the cmdlet should continue processing; otherwise, false.</returns>
    public new bool ShouldProcess(string target, string action) {
        ThrowIfStopped();
        _currentOutPipe?.Add(((target, action), PipelineType.ShouldProcess));
        return (bool)_currentReplyPipe?.Take(CancelToken)!;
    }

    /// <summary>
    /// Writes an object to the output pipeline.
    /// </summary>
    /// <param name="sendToPipeline">The object to send to the pipeline.</param>
    public new void WriteObject(object? sendToPipeline) => WriteObject(sendToPipeline, false);

    /// <summary>
    /// Writes an object to the output pipeline, optionally enumerating collections.
    /// </summary>
    /// <param name="sendToPipeline">The object to send to the pipeline.</param>
    /// <param name="enumerateCollection">If true, enumerates the collection.</param>
    public new void WriteObject(object? sendToPipeline, bool enumerateCollection) {
        ThrowIfStopped();
        _currentOutPipe?.Add(
            (sendToPipeline, enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output));
    }

    /// <summary>
    /// Writes an error record to the error pipeline.
    /// </summary>
    /// <param name="errorRecord">The error record to write.</param>
    public new void WriteError(ErrorRecord errorRecord) {
        ThrowIfStopped();
        _currentOutPipe?.Add((errorRecord, PipelineType.Error));
    }

    /// <summary>
    /// Writes a warning message to the warning pipeline.
    /// </summary>
    /// <param name="message">The warning message to write.</param>
    public new void WriteWarning(string message) {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Warning));
    }

    /// <summary>
    /// Writes a verbose message to the verbose pipeline.
    /// </summary>
    /// <param name="message">The verbose message to write.</param>
    public new void WriteVerbose(string message) {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Verbose));
    }

    /// <summary>
    /// Writes a debug message to the debug pipeline.
    /// </summary>
    /// <param name="message">The debug message to write.</param>
    public new void WriteDebug(string message) {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Debug));
    }

    /// <summary>
    /// Writes an information record to the information pipeline.
    /// </summary>
    /// <param name="informationRecord">The information record to write.</param>
    public new void WriteInformation(InformationRecord informationRecord) {
        ThrowIfStopped();
        _currentOutPipe?.Add((informationRecord, PipelineType.Information));
    }

    /// <summary>
    /// Writes a progress record to the progress pipeline.
    /// </summary>
    /// <param name="progressRecord">The progress record to write.</param>
    public new void WriteProgress(ProgressRecord progressRecord) {
        ThrowIfStopped();
        _currentOutPipe?.Add((progressRecord, PipelineType.Progress));
    }

    /// <summary>
    /// Throws a <see cref="PipelineStoppedException"/> if the cmdlet has been stopped.
    /// </summary>
    internal void ThrowIfStopped() {
        if (_cancelSource.IsCancellationRequested) {
            throw new PipelineStoppedException();
        }
    }

    /// <summary>
    /// Disposes the resources used by the cmdlet.
    /// </summary>
    public void Dispose() {
        _cancelSource?.Dispose();
    }
}