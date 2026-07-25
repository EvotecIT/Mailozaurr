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
public abstract partial class AsyncPSCmdlet : PSCmdlet, IDisposable
{
    private sealed class AsyncHookSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
            => ThreadPool.QueueUserWorkItem(_ => callback(state));
    }
    private sealed class AsyncHookTaskScheduler : TaskScheduler
    {
        protected override System.Collections.Generic.IEnumerable<Task>? GetScheduledTasks()
            => null;

        protected override void QueueTask(Task task)
            => ThreadPool.QueueUserWorkItem(_ => TryExecuteTask(task));

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            => TryExecuteTask(task);
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
        InformationWithTags,
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
    private static readonly TaskScheduler HookTaskScheduler = new AsyncHookTaskScheduler();
    private BlockingCollection<PipelineItem>? _currentOutPipe;
    private bool _cancelSourceDisposed;
    private bool _disposeRequested;
    private int _activeBlocks;
    private int _asyncLifecycleStarted;
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
        if (CanAccessPipelineDirectly)
            return base.ShouldProcess(target ?? string.Empty);

        return (bool)RequestPipelineReply(target ?? string.Empty, PipelineType.ShouldProcessTarget)!;
    }

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string? target, string action)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
            return base.ShouldProcess(target ?? string.Empty, action);

        return (bool)RequestPipelineReply((target ?? string.Empty, action), PipelineType.ShouldProcess)!;
    }

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
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
        if (CanAccessPipelineDirectly)
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
        if (CanAccessPipelineDirectly)
            return base.ShouldContinue(query, caption);

        return (bool)RequestPipelineReply((query, caption), PipelineType.ShouldContinue)!;
    }

    /// <summary>Thread-safe ShouldContinue bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldContinue(string query, string caption, ref bool yesToAll, ref bool noToAll)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
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
        if (CanAccessPipelineDirectly)
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
        if (CanAccessPipelineDirectly)
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
        if (CanAccessPipelineDirectly && Volatile.Read(ref _currentOutPipe) is null)
        {
            ThrowIfStopped();
            base.WriteObject(sendToPipeline, enumerateCollection);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(
            sendToPipeline,
            enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output));
    }

    /// <summary>Thread-safe error bridge for asynchronous cmdlet code.</summary>
    public new void WriteError(ErrorRecord errorRecord)
    {
        if (CanAccessPipelineDirectly && Volatile.Read(ref _currentOutPipe) is null)
        {
            ThrowIfStopped();
            base.WriteError(errorRecord);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(errorRecord, PipelineType.Error));
    }

    /// <summary>Thread-safe terminating-error bridge for asynchronous cmdlet code.</summary>
    public new void ThrowTerminatingError(ErrorRecord errorRecord)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
        {
            base.ThrowTerminatingError(errorRecord);
            return;
        }

        if (!TryQueue(new PipelineItem(errorRecord, PipelineType.TerminatingError)))
        {
            ThrowIfStopped();
            throw new InvalidOperationException(
                "No active PowerShell pipeline is available for the terminating error.");
        }

        throw new PipelineStoppedException();
    }

    /// <summary>Thread-safe warning bridge for asynchronous cmdlet code.</summary>
    public new void WriteWarning(string text)
    {
        if (CanAccessPipelineDirectly && Volatile.Read(ref _currentOutPipe) is null)
        {
            ThrowIfStopped();
            base.WriteWarning(text);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(text, PipelineType.Warning));
    }

    /// <summary>Thread-safe verbose bridge for asynchronous cmdlet code.</summary>
    public new void WriteVerbose(string text)
    {
        if (CanAccessPipelineDirectly && Volatile.Read(ref _currentOutPipe) is null)
        {
            ThrowIfStopped();
            base.WriteVerbose(text);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(text, PipelineType.Verbose));
    }

    /// <summary>Thread-safe debug bridge for asynchronous cmdlet code.</summary>
    public new void WriteDebug(string text)
    {
        if (CanAccessPipelineDirectly && Volatile.Read(ref _currentOutPipe) is null)
        {
            ThrowIfStopped();
            base.WriteDebug(text);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(text, PipelineType.Debug));
    }

    /// <summary>Thread-safe information bridge for asynchronous cmdlet code.</summary>
    public new void WriteInformation(InformationRecord informationRecord)
    {
        if (CanAccessPipelineDirectly && Volatile.Read(ref _currentOutPipe) is null)
        {
            ThrowIfStopped();
            base.WriteInformation(informationRecord);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(informationRecord, PipelineType.Information));
    }

    /// <summary>Thread-safe information bridge for asynchronous cmdlet code.</summary>
    public new void WriteInformation(object messageData, string[] tags)
    {
        if (CanAccessPipelineDirectly && Volatile.Read(ref _currentOutPipe) is null)
        {
            ThrowIfStopped();
            base.WriteInformation(messageData, tags);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(
            (messageData, (string[])tags.Clone()),
            PipelineType.InformationWithTags));
    }
}
