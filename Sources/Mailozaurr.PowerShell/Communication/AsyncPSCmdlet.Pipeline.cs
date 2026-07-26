using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

public abstract partial class AsyncPSCmdlet
{
    /// <summary>Thread-safe progress bridge for asynchronous cmdlet code.</summary>
    public new void WriteProgress(ProgressRecord progressRecord)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
            base.WriteProgress(progressRecord);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

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

    private bool CanAccessPipelineDirectly
        => IsPipelineThread ||
           (Volatile.Read(ref _asyncLifecycleStarted) == 0 &&
            Environment.CurrentManagedThreadId == _constructionThreadId);

    private void PrepareDirectPipelineAccess()
    {
        ThrowIfStopped();
        if (IsPipelineThread)
            Volatile.Read(ref _pumpQueuedItems)?.Invoke();
    }

    private void PrepareDirectPipelineInteraction()
    {
        ThrowIfStopped();
        ValidateInteractionGeneration();
        if (IsPipelineThread)
            Volatile.Read(ref _pumpQueuedItems)?.Invoke();
    }

    private void ValidateInteractionGeneration()
    {
        if (Volatile.Read(ref _asyncLifecycleStarted) == 0)
            return;

        var activeGeneration = Volatile.Read(ref _activeHookGeneration);
        var originatingGeneration = _hookGeneration.Value;
        if (activeGeneration == 0 && originatingGeneration == 0 && IsPipelineThread)
            return;

        if (originatingGeneration == 0 || originatingGeneration != activeGeneration)
        {
            throw new InvalidOperationException(
                "The asynchronous PowerShell lifecycle that originated this request is no longer active.");
        }
    }

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
        ValidateInteractionGeneration();
        var hookGeneration = _hookGeneration.Value;
        var replyPipe = new PipelineReplyChannel();
        if (!TryQueue(new PipelineItem(value, type, replyPipe, hookGeneration)))
        {
            replyPipe.Abandon();
            ThrowIfStopped();
            throw new InvalidOperationException("No active PowerShell pipeline is available for the asynchronous request.");
        }

        try
        {
            PipelineReply reply;
            try
            {
                reply = replyPipe.Take(CancelToken);
            }
            catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
            {
                throw new PipelineStoppedException();
            }

            if (reply.Rejection is not null)
                throw reply.Rejection;

            return reply.Value;
        }
        finally
        {
            replyPipe.ReleaseRequester();
        }
    }

    private bool TryQueue(PipelineItem item)
    {
        item.BindToHook(_hookGeneration.Value);
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
        var pumpingQueuedItems = 0;
        var hookGeneration = Interlocked.Increment(ref _nextHookGeneration);

        void ClearPipes()
        {
            Volatile.Write(ref _pumpQueuedItems, null);
            _ = Interlocked.CompareExchange(ref _currentOutPipe, null, outPipe);
            CompleteAddingIfNeeded(outPipe);
        }

        void DeactivateHook()
            => _ = Interlocked.CompareExchange(ref _activeHookGeneration, 0, hookGeneration);

        void DisposePipeOnce()
        {
            if (Interlocked.Exchange(ref pipeDisposed, 1) == 0)
            {
                while (outPipe.TryTake(out var abandonedItem))
                    abandonedItem.ReplyPipe?.Reject();
                outPipe.Dispose();
            }
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
            if (Volatile.Read(ref _asyncLifecycleStarted) != 0 &&
                item.HookGeneration != Volatile.Read(ref _activeHookGeneration))
            {
                item.ReplyPipe?.Reject();
                return;
            }

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
                case PipelineType.InformationWithTags:
                    var information = ((object MessageData, string[]? Tags))item.Value!;
                    base.WriteInformation(information.MessageData, information.Tags!);
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
                case PipelineType.PromptForCredentialOptions:
                    var promptOptions =
                        ((string Caption,
                            string Message,
                            string UserName,
                            string TargetName,
                            PSCredentialTypes AllowedCredentialTypes,
                            PSCredentialUIOptions Options))item.Value!;
                    item.ReplyPipe!.Publish(
                        () => Host.UI.PromptForCredential(
                            promptOptions.Caption,
                            promptOptions.Message,
                            promptOptions.UserName,
                            promptOptions.TargetName,
                            promptOptions.AllowedCredentialTypes,
                            promptOptions.Options));
                    break;
            }
        }

        void PumpQueuedItems()
        {
            if (Interlocked.Exchange(ref pumpingQueuedItems, 1) != 0)
                return;

            try
            {
                while (outPipe.TryTake(out var item))
                    PumpItem(item);
            }
            finally
            {
                Volatile.Write(ref pumpingQueuedItems, 0);
            }
        }

        Volatile.Write(ref _asyncLifecycleStarted, 1);
        _pipelineThreadId = Environment.CurrentManagedThreadId;
        Volatile.Write(ref _activeHookGeneration, hookGeneration);
        Volatile.Write(ref _pumpQueuedItems, PumpQueuedItems);
        Volatile.Write(ref _currentOutPipe, outPipe);

        var synchronizationContext = SynchronizationContext.Current;
        var priorHookGeneration = _hookGeneration.Value;
        try
        {
            SynchronizationContext.SetSynchronizationContext(HookSynchronizationContext);
            _hookGeneration.Value = hookGeneration;
            if (TaskScheduler.Current == TaskScheduler.Default)
            {
                blockTask = task();
            }
            else
            {
                using var invocationTask = new Task<Task>(
                    task,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach);
                invocationTask.RunSynchronously(HookTaskScheduler);
                blockTask = invocationTask.GetAwaiter().GetResult();
            }
        }
        catch (Exception exception)
        {
            try
            {
                PumpQueuedItems();
            }
            catch
            {
                // Preserve the hook failure after best-effort delivery of records written before it.
            }
            finally
            {
                ClearPipes();
                DeactivateHook();
                DisposePipeOnce();
            }

            if (exception is OperationCanceledException && _cancelSource.IsCancellationRequested)
                throw new PipelineStoppedException();

            throw;
        }
        finally
        {
            _hookGeneration.Value = priorHookGeneration;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        }

        if (blockTask.IsCompleted)
        {
            if (blockTask.IsFaulted)
                _ = blockTask.Exception;

            CompleteAddingIfNeeded(outPipe);
            try
            {
                PumpQueuedItems();
            }
            finally
            {
                ClearPipes();
                DeactivateHook();
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
            DeactivateHook();

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
            DeactivateHook();
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
