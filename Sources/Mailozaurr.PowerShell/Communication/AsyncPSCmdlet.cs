namespace Mailozaurr.PowerShell;

public abstract class AsyncPSCmdlet : PSCmdlet, IDisposable {
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

    private CancellationTokenSource _cancelSource = new();

    private BlockingCollection<(object?, PipelineType)>? _currentOutPipe;
    private BlockingCollection<object?>? _currentReplyPipe;

    protected internal CancellationToken CancelToken { get => _cancelSource.Token; }

    protected override void BeginProcessing()
        => RunBlockInAsync(BeginProcessingAsync);

    protected virtual Task BeginProcessingAsync()
        => Task.CompletedTask;

    protected override void ProcessRecord()
        => RunBlockInAsync(ProcessRecordAsync);

    protected virtual Task ProcessRecordAsync()
        => Task.CompletedTask;

    protected override void EndProcessing()
        => RunBlockInAsync(EndProcessingAsync);

    protected virtual Task EndProcessingAsync()
        => Task.CompletedTask;

    protected override void StopProcessing()
        => _cancelSource?.Cancel();

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

    public new bool ShouldProcess(string target, string action) {
        ThrowIfStopped();
        _currentOutPipe?.Add(((target, action), PipelineType.ShouldProcess));
        return (bool)_currentReplyPipe?.Take(CancelToken)!;
    }

    public new void WriteObject(object? sendToPipeline) => WriteObject(sendToPipeline, false);

    public new void WriteObject(object? sendToPipeline, bool enumerateCollection) {
        ThrowIfStopped();
        _currentOutPipe?.Add(
            (sendToPipeline, enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output));
    }

    public new void WriteError(ErrorRecord errorRecord) {
        ThrowIfStopped();
        _currentOutPipe?.Add((errorRecord, PipelineType.Error));
    }

    public new void WriteWarning(string message) {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Warning));
    }

    public new void WriteVerbose(string message) {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Verbose));
    }

    public new void WriteDebug(string message) {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Debug));
    }

    public new void WriteInformation(InformationRecord informationRecord) {
        ThrowIfStopped();
        _currentOutPipe?.Add((informationRecord, PipelineType.Information));
    }

    public new void WriteProgress(ProgressRecord progressRecord) {
        ThrowIfStopped();
        _currentOutPipe?.Add((progressRecord, PipelineType.Progress));
    }

    internal void ThrowIfStopped() {
        if (_cancelSource.IsCancellationRequested) {
            throw new PipelineStoppedException();
        }
    }

    public void Dispose() {
        _cancelSource?.Dispose();
    }
}