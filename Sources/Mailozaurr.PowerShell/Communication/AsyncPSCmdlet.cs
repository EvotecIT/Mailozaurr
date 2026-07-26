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
        CommandDetail,
        ShouldProcessTarget,
        ShouldProcess,
        ShouldProcessVerbose,
        ShouldProcessReason,
        ShouldContinue,
        ShouldContinueAll,
        ShouldContinueSecurity,
        PromptForCredential,
        PromptForCredentialOptions
    }

    private sealed class PipelineReply
    {
        public PipelineReply(object? value, Exception? rejection = null)
        {
            Value = value;
            Rejection = rejection;
        }

        public object? Value { get; }

        public Exception? Rejection { get; }
    }

    private sealed class PipelineReplyChannel
    {
        private readonly BlockingCollection<PipelineReply> _pipe = new(boundedCapacity: 1);
        private int _owners = 2;
        private int _pipelineOwner = 1;
        private int _requesterOwner = 1;

        public PipelineReply Take(CancellationToken cancellationToken)
            => _pipe.Take(cancellationToken);

        public void Publish(Func<object?> createValue)
            => PublishReply(() => new PipelineReply(createValue()));

        public void Reject()
            => PublishReply(
                () => new PipelineReply(
                    value: null,
                    new InvalidOperationException(
                        "The asynchronous PowerShell lifecycle that originated this request is no longer active.")));

        private void PublishReply(Func<PipelineReply> createReply)
        {
            try
            {
                if (Volatile.Read(ref _requesterOwner) == 0)
                    return;

                PipelineReply reply;
                try
                {
                    reply = createReply();
                }
                catch (Exception exception)
                {
                    TryPublish(new PipelineReply(value: null, exception));
                    throw;
                }

                TryPublish(reply);
            }
            finally
            {
                ReleasePipeline();
            }
        }

        private void TryPublish(PipelineReply reply)
        {
            try
            {
                _pipe.Add(reply);
            }
            catch (InvalidOperationException)
            {
                // The requester and pipeline can finish concurrently during cancellation.
            }
        }

        public void Abandon()
        {
            ReleaseRequester();
            ReleasePipeline();
        }

        public void ReleaseRequester()
        {
            if (Interlocked.Exchange(ref _requesterOwner, 0) == 1)
                Release();
        }

        public void ReleasePipeline()
        {
            if (Interlocked.Exchange(ref _pipelineOwner, 0) == 1)
                Release();
        }

        private void Release()
        {
            if (Interlocked.Decrement(ref _owners) == 0)
                _pipe.Dispose();
        }
    }

    private sealed class PipelineItem
    {
        public PipelineItem(
            object? value,
            PipelineType type,
            PipelineReplyChannel? replyPipe = null,
            long hookGeneration = 0)
        {
            Value = value;
            Type = type;
            ReplyPipe = replyPipe;
            HookGeneration = hookGeneration;
        }

        public object? Value { get; }

        public PipelineType Type { get; }

        public PipelineReplyChannel? ReplyPipe { get; }

        public long HookGeneration { get; private set; }

        public void BindToHook(long hookGeneration)
        {
            if (HookGeneration == 0)
                HookGeneration = hookGeneration;
        }
    }

    private readonly CancellationTokenSource _cancelSource = new();
    private readonly AsyncLocal<long> _hookGeneration = new();
    private readonly int _constructionThreadId = Environment.CurrentManagedThreadId;
    private readonly object _lifecycleLock = new();
    private static readonly SynchronizationContext HookSynchronizationContext = new AsyncHookSynchronizationContext();
    private static readonly TaskScheduler HookTaskScheduler = new AsyncHookTaskScheduler();
    private BlockingCollection<PipelineItem>? _currentOutPipe;
    private Action? _pumpQueuedItems;
    private long _activeHookGeneration;
    private long _nextHookGeneration;
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
        {
            PrepareDirectPipelineInteraction();
            return base.ShouldProcess(target ?? string.Empty);
        }

        return (bool)RequestPipelineReply(target ?? string.Empty, PipelineType.ShouldProcessTarget)!;
    }

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string? target, string action)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineInteraction();
            return base.ShouldProcess(target ?? string.Empty, action);
        }

        return (bool)RequestPipelineReply((target ?? string.Empty, action), PipelineType.ShouldProcess)!;
    }

    /// <summary>Thread-safe ShouldProcess bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineInteraction();
            return base.ShouldProcess(verboseDescription, verboseWarning, caption);
        }

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
        {
            PrepareDirectPipelineInteraction();
            return base.ShouldProcess(verboseDescription, verboseWarning, caption, out shouldProcessReason);
        }

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
        {
            PrepareDirectPipelineInteraction();
            return base.ShouldContinue(query, caption);
        }

        return (bool)RequestPipelineReply((query, caption), PipelineType.ShouldContinue)!;
    }

    /// <summary>Thread-safe ShouldContinue bridge for asynchronous cmdlet code.</summary>
    public new bool ShouldContinue(string query, string caption, ref bool yesToAll, ref bool noToAll)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineInteraction();
            return base.ShouldContinue(query, caption, ref yesToAll, ref noToAll);
        }

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
        {
            PrepareDirectPipelineInteraction();
            return base.ShouldContinue(query, caption, hasSecurityImpact, ref yesToAll, ref noToAll);
        }

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
        {
            PrepareDirectPipelineInteraction();
            return Host.UI.PromptForCredential(caption, message, userName, targetName);
        }

        return (PSCredential?)RequestPipelineReply(
            (caption, message, userName, targetName),
            PipelineType.PromptForCredential);
    }

    /// <summary>Thread-safe credential prompt bridge for asynchronous cmdlet code.</summary>
    public PSCredential? PromptForCredential(
        string caption,
        string message,
        string userName,
        string targetName,
        PSCredentialTypes allowedCredentialTypes,
        PSCredentialUIOptions options)
    {
        ThrowIfStopped();
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineInteraction();
            return Host.UI.PromptForCredential(
                caption,
                message,
                userName,
                targetName,
                allowedCredentialTypes,
                options);
        }

        return (PSCredential?)RequestPipelineReply(
            (caption, message, userName, targetName, allowedCredentialTypes, options),
            PipelineType.PromptForCredentialOptions);
    }

    /// <summary>Thread-safe output bridge for asynchronous cmdlet code.</summary>
    public new void WriteObject(object? sendToPipeline)
        => WriteObject(sendToPipeline, enumerateCollection: false);

    /// <summary>Thread-safe output bridge for asynchronous cmdlet code.</summary>
    public new void WriteObject(object? sendToPipeline, bool enumerateCollection)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
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
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
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
            PrepareDirectPipelineAccess();
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
    public new void WriteWarning(string message)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
            base.WriteWarning(message);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(message, PipelineType.Warning));
    }

    /// <summary>Thread-safe verbose bridge for asynchronous cmdlet code.</summary>
    public new void WriteVerbose(string message)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
            base.WriteVerbose(message);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(message, PipelineType.Verbose));
    }

    /// <summary>Thread-safe debug bridge for asynchronous cmdlet code.</summary>
    public new void WriteDebug(string message)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
            base.WriteDebug(message);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(message, PipelineType.Debug));
    }

    /// <summary>Thread-safe command-detail bridge for asynchronous cmdlet code.</summary>
    public new void WriteCommandDetail(string text)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
            base.WriteCommandDetail(text);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(text, PipelineType.CommandDetail));
    }

    /// <summary>Thread-safe information bridge for asynchronous cmdlet code.</summary>
    public new void WriteInformation(InformationRecord informationRecord)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
            base.WriteInformation(informationRecord);
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(informationRecord, PipelineType.Information));
    }

    /// <summary>Thread-safe information bridge for asynchronous cmdlet code.</summary>
    public new void WriteInformation(object messageData, string[]? tags)
    {
        if (CanAccessPipelineDirectly)
        {
            PrepareDirectPipelineAccess();
            base.WriteInformation(messageData, tags ?? Array.Empty<string>());
            return;
        }

        if (Volatile.Read(ref _currentOutPipe) is null)
            return;

        _ = TryQueue(new PipelineItem(
            (messageData, tags is null ? null : (string[])tags.Clone()),
            PipelineType.InformationWithTags));
    }
}
