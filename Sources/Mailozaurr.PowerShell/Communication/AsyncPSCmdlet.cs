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
    }

    private CancellationTokenSource _cancelSource = new();

    private BlockingCollection<(object?, PipelineType)>? _currentPipe;

    protected CancellationToken CancelToken { get => _cancelSource.Token; }

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
        using BlockingCollection<(object?, PipelineType)> pipe = new();
        Task blockTask = Task.Run(async () => {
            try {
                _currentPipe = pipe;
                await task();
            } finally {
                _currentPipe = null;
                pipe.CompleteAdding();
            }
        });

        foreach ((object? data, PipelineType pipelineType) in pipe.GetConsumingEnumerable()) {
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
            }
        }

        blockTask.GetAwaiter().GetResult();
    }

    public new void WriteObject(object? sendToPipeline) => WriteObject(sendToPipeline, false);

    public new void WriteObject(object? sendToPipeline, bool enumerateCollection) {
        ThrowIfStopped();
        _currentPipe?.Add(
            (sendToPipeline, enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output));
    }

    public new void WriteError(ErrorRecord errorRecord) {
        ThrowIfStopped();
        _currentPipe?.Add((errorRecord, PipelineType.Error));
    }

    public new void WriteWarning(string message) {
        ThrowIfStopped();
        _currentPipe?.Add((message, PipelineType.Warning));
    }

    public new void WriteVerbose(string message) {
        ThrowIfStopped();
        _currentPipe?.Add((message, PipelineType.Verbose));
    }

    public new void WriteDebug(string message) {
        ThrowIfStopped();
        _currentPipe?.Add((message, PipelineType.Debug));
    }

    public new void WriteInformation(InformationRecord informationRecord) {
        ThrowIfStopped();
        _currentPipe?.Add((informationRecord, PipelineType.Information));
    }

    public new void WriteProgress(ProgressRecord progressRecord) {
        ThrowIfStopped();
        _currentPipe?.Add((progressRecord, PipelineType.Progress));
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
