using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>Suppresses delivery attempts for a configured interval after consecutive failures, with state local to each instance.</summary>
public sealed class MailDeliveryCooldown {
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SemaphoreSlim _execution = new SemaphoreSlim(1, 1);
    private readonly object _stateLock = new object();
    private int _failureCount;
    private DateTimeOffset? _cooldownUntilUtc;

    /// <summary>Initializes a delivery cooldown using an optional clock for application-controlled time.</summary>
    /// <param name="utcNow">The current time provider. Defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    public MailDeliveryCooldown(Func<DateTimeOffset>? utcNow = null) {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Executes an action unless a cooldown is active; enabled attempts are serialized per instance.</summary>
    /// <param name="action">The delivery action.</param>
    /// <param name="threshold">Consecutive failures before suppression. Zero or negative disables cooldown handling.</param>
    /// <param name="window">Suppression duration. Zero or negative disables cooldown handling.</param>
    /// <param name="cancellationToken">Token passed to the action and used to cancel waiting.</param>
    /// <returns>A task that completes when the action succeeds.</returns>
    /// <exception cref="InvalidOperationException">A cooldown is active and the action was suppressed.</exception>
    /// <remarks>Failures propagate to the caller. Cancellation and suppressed attempts do not count as failures.
    /// Disabled calls bypass all cooldown state, including resets. Success and expiry reset consecutive failures.</remarks>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, int threshold, TimeSpan window, CancellationToken cancellationToken = default) {
        if (action is null) {
            throw new ArgumentNullException(nameof(action));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (threshold <= 0 || window <= TimeSpan.Zero) {
            await action(cancellationToken).ConfigureAwait(false);
            return;
        }

        await _execution.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now = _utcNow().ToUniversalTime();
            lock (_stateLock) {
                if (_cooldownUntilUtc.HasValue) {
                    if (now < _cooldownUntilUtc.Value) {
                        throw new InvalidOperationException("Mail delivery cooldown is active.");
                    }
                    _failureCount = 0;
                    _cooldownUntilUtc = null;
                }
            }

            try {
                await action(cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch {
                DateTimeOffset failedAt = _utcNow().ToUniversalTime();
                lock (_stateLock) {
                    _failureCount++;
                    if (_failureCount >= threshold) {
                        TimeSpan available = DateTimeOffset.MaxValue - failedAt;
                        _cooldownUntilUtc = window >= available ? DateTimeOffset.MaxValue : failedAt.Add(window);
                    }
                }
                throw;
            }

            lock (_stateLock) {
                _failureCount = 0;
                _cooldownUntilUtc = null;
            }
        } finally {
            _execution.Release();
        }
    }

    /// <summary>Returns a consistent immutable snapshot without changing cooldown state.</summary>
    /// <returns>The current failure count and recorded UTC cooldown expiry.</returns>
    public MailDeliveryCooldownSnapshot GetSnapshot() {
        lock (_stateLock) {
            return new MailDeliveryCooldownSnapshot(_failureCount, _cooldownUntilUtc);
        }
    }
}
