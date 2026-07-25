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
        ShouldProcessTarget,
        ShouldProcess,
        ShouldProcessVerbose,
        ShouldProcessReason,
        ShouldContinue,
        ShouldContinueAll,
        ShouldContinueSecurity,
        PromptForCredential
    }

    private sealed class PipelineReply
    {
        public PipelineReply(object? value)
            => Value = value;

        public object? Value { get; }
    }

    private sealed class PipelineReplyChannel
    {
        private readonly BlockingCollection<PipelineReply> _pipe = new(boundedCapacity: 1);
        private int _owners = 2;

        public PipelineReply Take(CancellationToken cancellationToken)
            => _pipe.Take(cancellationToken);

        public void Publish(Func<object?> createValue)
        {
            try
            {
                var reply = new PipelineReply(createValue());
                try
                {
                    _pipe.Add(reply);
                }
                catch (InvalidOperationException)
                {
                    // The requester and pipeline can finish concurrently during cancellation.
                }
            }
            finally
            {
                Release();
            }
        }

        public void Abandon()
        {
            Release();
            Release();
        }

        public void ReleaseRequester()
            => Release();

        private void Release()
        {
            if (Interlocked.Decrement(ref _owners) == 0)
                _pipe.Dispose();
        }
    }

    private sealed class PipelineItem
    {
        public PipelineItem(object? value, PipelineType type, PipelineReplyChannel? replyPipe = null)
        {
            Value = value;
            Type = type;
            ReplyPipe = replyPipe;
        }

        public object? Value { get; }

        public PipelineType Type { get; }

        public PipelineReplyChannel? ReplyPipe { get; }
    }

    private readonly CancellationTokenSource _cancelSource = new();
    private readonly object _lifecycleLock = new();
    private static readonly SynchronizationContext HookSynchronizationContext = new AsyncHookSynchronizationContext();
    private BlockingCollection<PipelineItem>? _currentOutPipe;
    private bool _cancelSourceDisposed;
    private bool _disposeRequested;
    private int _activeBlocks;
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
        => CancelSource();

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string? target)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return base.ShouldProcess(target ?? string.Empty);

        return (bool)RequestPipelineReply(target ?? string.Empty, PipelineType.ShouldProcessTarget)!;
    }

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string? target, string action)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return base.ShouldProcess(target ?? string.Empty, action);

        return (bool)RequestPipelineReply((target ?? string.Empty, action), PipelineType.ShouldProcess)!;
    }

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return base.ShouldProcess(verboseDescription, verboseWarning, caption);

        return (bool)RequestPipelineReply(
            (verboseDescription, verboseWarning, caption),
            PipelineType.ShouldProcessVerbose)!;
    }

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(
        string verboseDescription,
        string verboseWarning,
        string caption,
        out ShouldProcessReason shouldProcessReason)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return base.ShouldProcess(verboseDescription, verboseWarning, caption, out shouldProcessReason);

        var reply = ((bool Result, ShouldProcessReason Reason))RequestPipelineReply(
            (verboseDescription, verboseWarning, caption),
            PipelineType.ShouldProcessReason)!;
        shouldProcessReason = reply.Reason;
        return reply.Result;
    }

    /// <summary>Thread-safe ShouldContinue bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldContinue(string query, string caption)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return base.ShouldContinue(query, caption);

        return (bool)RequestPipelineReply((query, caption), PipelineType.ShouldContinue)!;
    }

    /// <summary>Thread-safe ShouldContinue bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldContinue(string query, string caption, ref bool yesToAll, ref bool noToAll)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return base.ShouldContinue(query, caption, ref yesToAll, ref noToAll);

        var reply = ((bool Result, bool YesToAll, bool NoToAll))RequestPipelineReply(
            (query, caption, yesToAll, noToAll),
            PipelineType.ShouldContinueAll)!;
        yesToAll = reply.YesToAll;
        noToAll = reply.NoToAll;
        return reply.Result;
    }

    /// <summary>Thread-safe ShouldContinue bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldContinue(
        string query,
        string caption,
        bool hasSecurityImpact,
        ref bool yesToAll,
        ref bool noToAll)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return base.ShouldContinue(query, caption, hasSecurityImpact, ref yesToAll, ref noToAll);

        var reply = ((bool Result, bool YesToAll, bool NoToAll))RequestPipelineReply(
            (query, caption, hasSecurityImpact, yesToAll, noToAll),
            PipelineType.ShouldContinueSecurity)!;
        yesToAll = reply.YesToAll;
        noToAll = reply.NoToAll;
        return reply.Result;
    }

    /// <summary>Thread-safe credential prompt bridge for asynchronous cmdlet code.</summary>
    public PSCredential? PromptForCredential(string caption, string message, string userName, string targetName)
    {
        ThrowIfStopped();
        if (IsPipelineThread || Volatile.Read(ref _currentOutPipe) is null)
            return Host.UI.PromptForCredential(caption, message, userName, targetName);

        return (PSCredential?)RequestPipelineReply(
            (caption, message, userName, targetName),
            PipelineType.PromptForCredential);
    }

    /// <summary>Thread-safe output bridge for asynchronous cmdlet code.</summary>
    public new void WriteObject(object? sendToPipeline)
        => WriteObject(sendToPipeline, enumerateCollection: false);

    /// <summary>Thread-safe output bridge for asynchronous cmdlet code.</summary>
    public new void WriteObject(object? sendToPipeline, bool enumerateCollection)
    {
        if (!IsPipelineThread && Volatile.Read(ref _currentOutPipe) is null)
            return;

        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.WriteObject(sendToPipeline, enumerateCollection);
            return;
        }

        _ = TryQueue(new PipelineItem(
            sendToPipeline,
            enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output));
    }

    /// <summary>Thread-safe error bridge for asynchronous cmdlet code.</summary>
    public new void WriteError(ErrorRecord errorRecord)
    {
        if (!IsPipelineThread && Volatile.Read(ref _currentOutPipe) is null)
            return;

        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.WriteError(errorRecord);
            return;
        }

        _ = TryQueue(new PipelineItem(errorRecord, PipelineType.Error));
    }

    /// <summary>Thread-safe terminating-error bridge for asynchronous cmdlet code.</summary>
    public new void ThrowTerminatingError(ErrorRecord errorRecord)
    {
        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.ThrowTerminatingError(errorRecord);
            return;
        }

        _ = TryQueue(new PipelineItem(errorRecord, PipelineType.TerminatingError));
        throw new PipelineStoppedException();
    }

    /// <summary>Thread-safe warning bridge for asynchronous cmdlet code.</summary>
    public new void WriteWarning(string text)
    {
        if (!IsPipelineThread && Volatile.Read(ref _currentOutPipe) is null)
            return;

        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.WriteWarning(text);
            return;
        }

        _ = TryQueue(new PipelineItem(text, PipelineType.Warning));
    }

    /// <summary>Thread-safe verbose bridge for asynchronous cmdlet code.</summary>
    public new void WriteVerbose(string text)
    {
        if (!IsPipelineThread && Volatile.Read(ref _currentOutPipe) is null)
            return;

        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.WriteVerbose(text);
            return;
        }

        _ = TryQueue(new PipelineItem(text, PipelineType.Verbose));
    }

    /// <summary>Thread-safe debug bridge for asynchronous cmdlet code.</summary>
    public new void WriteDebug(string text)
    {
        if (!IsPipelineThread && Volatile.Read(ref _currentOutPipe) is null)
            return;

        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.WriteDebug(text);
            return;
        }

        _ = TryQueue(new PipelineItem(text, PipelineType.Debug));
    }

    /// <summary>Thread-safe information bridge for asynchronous cmdlet code.</summary>
    public new void WriteInformation(InformationRecord informationRecord)
    {
        if (!IsPipelineThread && Volatile.Read(ref _currentOutPipe) is null)
            return;

        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.WriteInformation(informationRecord);
            return;
        }

        _ = TryQueue(new PipelineItem(informationRecord, PipelineType.Information));
    }

    /// <summary>Thread-safe progress bridge for asynchronous cmdlet code.</summary>
    public new void WriteProgress(ProgressRecord progressRecord)
    {
        if (!IsPipelineThread && Volatile.Read(ref _currentOutPipe) is null)
            return;

        ThrowIfStopped();
        if (IsPipelineThread)
        {
            base.WriteProgress(progressRecord);
            return;
        }

        _ = TryQueue(new PipelineItem(progressRecord, PipelineType.Progress));
    }

    /// <summary>Throws when PowerShell has requested cancellation.</summary>
    protected internal void ThrowIfStopped()
    {
        if (_cancelSource.IsCancellationRequested)
            throw new PipelineStoppedException();
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposeRequested)
                return;

            _disposeRequested = true;
        }

        CancelSource();

        lock (_lifecycleLock)
        {
            DisposeCancelSourceIfInactive();
        }

        _pipelineThreadId = 0;
    }

    private bool IsPipelineThread
        => _pipelineThreadId != 0 && Environment.CurrentManagedThreadId == _pipelineThreadId;

    private void GetBlockTaskResult(Task blockTask)
    {
        try
        {
            blockTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
        {
            throw new PipelineStoppedException();
        }
    }

    private object? RequestPipelineReply(object? value, PipelineType type)
    {
        ThrowIfStopped();
        var replyPipe = new PipelineReplyChannel();
        if (!TryQueue(new PipelineItem(value, type, replyPipe)))
        {
            replyPipe.Abandon();
            ThrowIfStopped();
            throw new InvalidOperationException("No active PowerShell pipeline is available for the asynchronous request.");
        }

        try
        {
            return replyPipe.Take(CancelToken).Value;
        }
        finally
        {
            replyPipe.ReleaseRequester();
        }
    }

    private bool TryQueue(PipelineItem item)
    {
        var outPipe = Volatile.Read(ref _currentOutPipe);
        if (outPipe is null)
            return false;

        try
        {
            outPipe.Add(item, CancelToken);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
        {
            return false;
        }
    }

    private void RunBlockInAsync(Func<Task> task)
    {
        EnterAsyncBlock();
        try
        {
            RunBlockInAsyncCore(task);
        }
        finally
        {
            ExitAsyncBlock();
        }
    }

    private void RunBlockInAsyncCore(Func<Task> task)
    {
        var outPipe = new BlockingCollection<PipelineItem>();
        Task blockTask;
        var deferPipeDisposal = 0;
        var pipeDisposed = 0;

        void ClearPipes()
        {
            _ = Interlocked.CompareExchange(ref _currentOutPipe, null, outPipe);
            CompleteAddingIfNeeded(outPipe);
        }

        void DisposePipeOnce()
        {
            if (Interlocked.Exchange(ref pipeDisposed, 1) == 0)
                outPipe.Dispose();
        }

        static void CompleteAddingIfNeeded<T>(BlockingCollection<T> pipe)
        {
            try
            {
                if (!pipe.IsAddingCompleted)
                    pipe.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
                // A deferred worker may race the one-time disposal after a pipeline failure.
            }
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
                case PipelineType.ShouldProcessTarget:
                    item.ReplyPipe!.Publish(
                        () => base.ShouldProcess((string)item.Value!));
                    break;
                case PipelineType.ShouldProcess:
                    var should = ((string Target, string Action))item.Value!;
                    item.ReplyPipe!.Publish(
                        () => base.ShouldProcess(should.Target, should.Action));
                    break;
                case PipelineType.ShouldProcessVerbose:
                    var verbose = ((string Description, string Warning, string Caption))item.Value!;
                    item.ReplyPipe!.Publish(
                        () => base.ShouldProcess(verbose.Description, verbose.Warning, verbose.Caption));
                    break;
                case PipelineType.ShouldProcessReason:
                    var reasonRequest = ((string Description, string Warning, string Caption))item.Value!;
                    item.ReplyPipe!.Publish(() =>
                    {
                        var result = base.ShouldProcess(
                            reasonRequest.Description,
                            reasonRequest.Warning,
                            reasonRequest.Caption,
                            out var reason);
                        return (result, reason);
                    });
                    break;
                case PipelineType.ShouldContinue:
                    var shouldContinue = ((string Query, string Caption))item.Value!;
                    item.ReplyPipe!.Publish(
                        () => base.ShouldContinue(shouldContinue.Query, shouldContinue.Caption));
                    break;
                case PipelineType.ShouldContinueAll:
                    var shouldContinueAll =
                        ((string Query, string Caption, bool YesToAll, bool NoToAll))item.Value!;
                    item.ReplyPipe!.Publish(() =>
                    {
                        var yesToAll = shouldContinueAll.YesToAll;
                        var noToAll = shouldContinueAll.NoToAll;
                        var continueAll = base.ShouldContinue(
                            shouldContinueAll.Query,
                            shouldContinueAll.Caption,
                            ref yesToAll,
                            ref noToAll);
                        return (continueAll, yesToAll, noToAll);
                    });
                    break;
                case PipelineType.ShouldContinueSecurity:
                    var shouldContinueSecurity =
                        ((string Query, string Caption, bool HasSecurityImpact, bool YesToAll, bool NoToAll))item.Value!;
                    item.ReplyPipe!.Publish(() =>
                    {
                        var yesToAll = shouldContinueSecurity.YesToAll;
                        var noToAll = shouldContinueSecurity.NoToAll;
                        var continueSecurity = base.ShouldContinue(
                            shouldContinueSecurity.Query,
                            shouldContinueSecurity.Caption,
                            shouldContinueSecurity.HasSecurityImpact,
                            ref yesToAll,
                            ref noToAll);
                        return (continueSecurity, yesToAll, noToAll);
                    });
                    break;
                case PipelineType.PromptForCredential:
                    var prompt = ((string Caption, string Message, string UserName, string TargetName))item.Value!;
                    item.ReplyPipe!.Publish(
                        () => Host.UI.PromptForCredential(
                            prompt.Caption,
                            prompt.Message,
                            prompt.UserName,
                            prompt.TargetName));
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
        catch (Exception exception)
        {
            ClearPipes();
            DisposePipeOnce();

            if (exception is OperationCanceledException && _cancelSource.IsCancellationRequested)
                throw new PipelineStoppedException();

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
                DisposePipeOnce();
            }

            GetBlockTaskResult(blockTask);
            return;
        }

        RetainAsyncBlock();
        try
        {
            _ = blockTask.ContinueWith(
                completed =>
                {
                    try
                    {
                        if (completed.IsFaulted)
                            _ = completed.Exception;

                        ClearPipes();
                        if (Volatile.Read(ref deferPipeDisposal) != 0)
                            DisposePipeOnce();
                    }
                    finally
                    {
                        ExitAsyncBlock();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch
        {
            ExitAsyncBlock();
            throw;
        }

        try
        {
            foreach (var item in outPipe.GetConsumingEnumerable(CancelToken))
            {
                PumpItem(item);
            }
        }
        catch (Exception pipelineException)
        {
            var stopRequested = _cancelSource.IsCancellationRequested;
            Volatile.Write(ref deferPipeDisposal, 1);
            CancelSource();
            CompleteAddingIfNeeded(outPipe);
            if (blockTask.IsCompleted)
                DisposePipeOnce();

            if (pipelineException is OperationCanceledException && stopRequested)
                throw new PipelineStoppedException();

            throw;
        }

        try
        {
            GetBlockTaskResult(blockTask);
        }
        finally
        {
            DisposePipeOnce();
        }
    }

    private void EnterAsyncBlock()
    {
        lock (_lifecycleLock)
        {
            if (_disposeRequested)
                throw new ObjectDisposedException(GetType().FullName);

            _activeBlocks++;
        }
    }

    private void ExitAsyncBlock()
    {
        lock (_lifecycleLock)
        {
            _activeBlocks--;
            DisposeCancelSourceIfInactive();
        }
    }

    private void RetainAsyncBlock()
    {
        lock (_lifecycleLock)
        {
            _activeBlocks++;
        }
    }

    private void CancelSource()
    {
        try
        {
            _cancelSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Disposal may race a late StopProcessing callback after all async hooks have exited.
        }
    }

    private void DisposeCancelSourceIfInactive()
    {
        if (!_disposeRequested || _activeBlocks != 0 || _cancelSourceDisposed)
            return;

        _cancelSource.Dispose();
        _cancelSourceDisposed = true;
    }
}
