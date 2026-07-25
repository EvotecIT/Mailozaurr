using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Base class for cmdlets that await asynchronous engine work while routing PowerShell pipeline writes
/// back through the synchronous cmdlet pipeline thread.
/// </summary>
/// <remarks>
/// Invoke asynchronous hooks on the PowerShell pipeline thread until their first incomplete await.
/// The base temporarily replaces the host synchronization context with an internal thread-pool
/// context while invoking each hook. This prevents continuations from capturing either the host
/// context or a custom task scheduler that may be running the PowerShell pipeline thread.
/// Keep hook implementations asynchronous all the way through and pass <see cref="CancelToken"/> to
/// cancellable engine operations. Do not block with Task.Wait, Task.Result, or Task.WaitAll.
/// </remarks>
public abstract class AsyncPSCmdlet : PSCmdlet, IDisposable
{
    private sealed class AsyncHookSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
            => ThreadPool.QueueUserWorkItem(_ => callback(state));
    }

    private enum PipelineType
    {
        Output,
        OutputEnumerate,
        Error,
        TerminatingError,
        Warning,
        Verbose,
        Debug,
        Information,
        Progress,
        ShouldProcess,
        ShouldContinue,
        PromptForCredential
    }

    private sealed class PipelineItem
    {
        public PipelineItem(object? value, PipelineType type, BlockingCollection<object?>? replyPipe = null)
        {
            Value = value;
            Type = type;
            ReplyPipe = replyPipe;
        }

        public object? Value { get; }

        public PipelineType Type { get; }

        public BlockingCollection<object?>? ReplyPipe { get; }
    }

    private readonly CancellationTokenSource _cancelSource = new();
    private static readonly SynchronizationContext HookSynchronizationContext = new AsyncHookSynchronizationContext();
    private BlockingCollection<PipelineItem>? _currentOutPipe;
    private int _pipelineThreadId;

    /// <summary>Cancellation token triggered when PowerShell stops the cmdlet.</summary>
    protected internal CancellationToken CancelToken => _cancelSource.Token;

    /// <inheritdoc />
    protected override void BeginProcessing()
        => RunBlockInAsync(BeginProcessingAsync);

    /// <summary>Asynchronous begin hook.</summary>
    protected virtual Task BeginProcessingAsync()
        => Task.CompletedTask;

    /// <inheritdoc />
    protected override void ProcessRecord()
        => RunBlockInAsync(ProcessRecordAsync);

    /// <summary>Asynchronous process-record hook.</summary>
    protected virtual Task ProcessRecordAsync()
        => Task.CompletedTask;

    /// <inheritdoc />
    protected override void EndProcessing()
        => RunBlockInAsync(EndProcessingAsync);

    /// <summary>Asynchronous end hook.</summary>
    protected virtual Task EndProcessingAsync()
        => Task.CompletedTask;

    /// <inheritdoc />
    protected override void StopProcessing()
        => _cancelSource.Cancel();

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string? target, string action)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
            return base.ShouldProcess(target ?? string.Empty, action);

        using var replyPipe = new BlockingCollection<object?>(boundedCapacity: 1);
        _currentOutPipe.Add(new PipelineItem((target ?? string.Empty, action), PipelineType.ShouldProcess, replyPipe), CancelToken);
        return (bool)replyPipe.Take(CancelToken)!;
    }

    /// <summary>Thread-safe ShouldContinue bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldContinue(string query, string caption)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
            return base.ShouldContinue(query, caption);

        using var replyPipe = new BlockingCollection<object?>(boundedCapacity: 1);
        _currentOutPipe.Add(new PipelineItem((query, caption), PipelineType.ShouldContinue, replyPipe), CancelToken);
        return (bool)replyPipe.Take(CancelToken)!;
    }

    /// <summary>Thread-safe credential prompt bridge for asynchronous cmdlet code.</summary>
    public PSCredential? PromptForCredential(string caption, string message, string userName, string targetName)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
            return Host.UI.PromptForCredential(caption, message, userName, targetName);

        using var replyPipe = new BlockingCollection<object?>(boundedCapacity: 1);
        _currentOutPipe.Add(new PipelineItem((caption, message, userName, targetName), PipelineType.PromptForCredential, replyPipe), CancelToken);
        return (PSCredential?)replyPipe.Take(CancelToken);
    }

    /// <summary>Thread-safe output bridge for asynchronous cmdlet code.</summary>
    public new void WriteObject(object? sendToPipeline)
        => WriteObject(sendToPipeline, enumerateCollection: false);

    /// <summary>Thread-safe output bridge for asynchronous cmdlet code.</summary>
    public new void WriteObject(object? sendToPipeline, bool enumerateCollection)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.WriteObject(sendToPipeline, enumerateCollection);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(sendToPipeline, enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output), CancelToken);
    }

    /// <summary>Thread-safe error bridge for asynchronous cmdlet code.</summary>
    public new void WriteError(ErrorRecord errorRecord)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.WriteError(errorRecord);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(errorRecord, PipelineType.Error), CancelToken);
    }

    /// <summary>Thread-safe terminating-error bridge for asynchronous cmdlet code.</summary>
    protected new void ThrowTerminatingError(ErrorRecord errorRecord)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.ThrowTerminatingError(errorRecord);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(errorRecord, PipelineType.TerminatingError), CancelToken);
        throw new PipelineStoppedException();
    }

    /// <summary>Thread-safe warning bridge for asynchronous cmdlet code.</summary>
    public new void WriteWarning(string text)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.WriteWarning(text);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(text, PipelineType.Warning), CancelToken);
    }

    /// <summary>Thread-safe verbose bridge for asynchronous cmdlet code.</summary>
    public new void WriteVerbose(string text)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.WriteVerbose(text);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(text, PipelineType.Verbose), CancelToken);
    }

    /// <summary>Thread-safe debug bridge for asynchronous cmdlet code.</summary>
    public new void WriteDebug(string text)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.WriteDebug(text);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(text, PipelineType.Debug), CancelToken);
    }

    /// <summary>Thread-safe information bridge for asynchronous cmdlet code.</summary>
    public new void WriteInformation(InformationRecord informationRecord)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.WriteInformation(informationRecord);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(informationRecord, PipelineType.Information), CancelToken);
    }

    /// <summary>Thread-safe progress bridge for asynchronous cmdlet code.</summary>
    public new void WriteProgress(ProgressRecord progressRecord)
    {
        ThrowIfStopped();
        if (_currentOutPipe is null || IsPipelineThread)
        {
            base.WriteProgress(progressRecord);
            return;
        }

        _currentOutPipe.Add(new PipelineItem(progressRecord, PipelineType.Progress), CancelToken);
    }

    /// <summary>Throws when PowerShell has requested cancellation.</summary>
    protected internal void ThrowIfStopped()
    {
        if (_cancelSource.IsCancellationRequested)
            throw new PipelineStoppedException();
    }

    /// <inheritdoc />
    public virtual void Dispose()
        => _cancelSource.Dispose();

    private bool IsPipelineThread
        => _pipelineThreadId != 0 && Environment.CurrentManagedThreadId == _pipelineThreadId;

    private void RunBlockInAsync(Func<Task> task)
    {
        using var outPipe = new BlockingCollection<PipelineItem>();
        Task blockTask;

        void ClearPipes()
        {
            _currentOutPipe = null;
            _pipelineThreadId = 0;
            CompleteAddingIfNeeded(outPipe);
        }

        static void CompleteAddingIfNeeded<T>(BlockingCollection<T> pipe)
        {
            if (!pipe.IsAddingCompleted)
                pipe.CompleteAdding();
        }

        void PumpItem(PipelineItem item)
        {
            switch (item.Type)
            {
                case PipelineType.Output:
                    base.WriteObject(item.Value);
                    break;
                case PipelineType.OutputEnumerate:
                    base.WriteObject(item.Value, enumerateCollection: true);
                    break;
                case PipelineType.Error:
                    base.WriteError((ErrorRecord)item.Value!);
                    break;
                case PipelineType.TerminatingError:
                    base.ThrowTerminatingError((ErrorRecord)item.Value!);
                    break;
                case PipelineType.Warning:
                    base.WriteWarning((string)item.Value!);
                    break;
                case PipelineType.Verbose:
                    base.WriteVerbose((string)item.Value!);
                    break;
                case PipelineType.Debug:
                    base.WriteDebug((string)item.Value!);
                    break;
                case PipelineType.Information:
                    base.WriteInformation((InformationRecord)item.Value!);
                    break;
                case PipelineType.Progress:
                    base.WriteProgress((ProgressRecord)item.Value!);
                    break;
                case PipelineType.ShouldProcess:
                    var should = ((string Target, string Action))item.Value!;
                    item.ReplyPipe!.Add(base.ShouldProcess(should.Target, should.Action), CancelToken);
                    break;
                case PipelineType.ShouldContinue:
                    var shouldContinue = ((string Query, string Caption))item.Value!;
                    item.ReplyPipe!.Add(base.ShouldContinue(shouldContinue.Query, shouldContinue.Caption), CancelToken);
                    break;
                case PipelineType.PromptForCredential:
                    var prompt = ((string Caption, string Message, string UserName, string TargetName))item.Value!;
                    item.ReplyPipe!.Add(
                        Host.UI.PromptForCredential(prompt.Caption, prompt.Message, prompt.UserName, prompt.TargetName),
                        CancelToken);
                    break;
            }
        }

        void PumpQueuedItems()
        {
            while (outPipe.TryTake(out var item))
                PumpItem(item);
        }

        _pipelineThreadId = Environment.CurrentManagedThreadId;
        _currentOutPipe = outPipe;

        var synchronizationContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(HookSynchronizationContext);
            blockTask = task();
        }
        catch
        {
            ClearPipes();
            throw;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        }

        if (blockTask.IsCompleted)
        {
            CompleteAddingIfNeeded(outPipe);
            try
            {
                PumpQueuedItems();
            }
            finally
            {
                ClearPipes();
            }

            blockTask.GetAwaiter().GetResult();
            return;
        }

        _ = blockTask.ContinueWith(
            completed => ClearPipes(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            foreach (var item in outPipe.GetConsumingEnumerable(CancelToken))
            {
                PumpItem(item);
            }
        }
        catch
        {
            _cancelSource.Cancel();
            CompleteAddingIfNeeded(outPipe);
            try
            {
                blockTask.GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is OperationCanceledException or PipelineStoppedException)
            {
            }

            throw;
        }

        blockTask.GetAwaiter().GetResult();
    }
}
