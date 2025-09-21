using System;

namespace Mailozaurr;

/// <summary>
/// Configures how <see cref="GraphMessageListener"/> retains identifiers of processed messages.
/// </summary>
public class GraphMessageListenerRetentionOptions {
    private static readonly Func<DateTimeOffset> DefaultClock = () => DateTimeOffset.UtcNow;
    private int? _maxSeenIds = 1024;
    private TimeSpan? _slidingExpiration;
    private Func<DateTimeOffset>? _clock;

    /// <summary>
    /// Gets or sets the maximum number of message identifiers that should be retained.
    /// </summary>
    /// <remarks>
    /// When the limit is exceeded, the oldest identifiers are evicted first.
    /// Specify <c>null</c> to disable the limit.
    /// </remarks>
    public int? MaxSeenIds {
        get => _maxSeenIds;
        set {
            if (value.HasValue && value.Value <= 0) {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxSeenIds must be greater than zero.");
            }

            _maxSeenIds = value;
        }
    }

    /// <summary>
    /// Gets or sets the sliding expiration applied to message identifiers.
    /// </summary>
    /// <remarks>
    /// Identifiers older than the configured window are removed.
    /// Specify <c>null</c> to disable the expiration.
    /// </remarks>
    public TimeSpan? SlidingExpiration {
        get => _slidingExpiration;
        set {
            if (value.HasValue && value.Value <= TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(value), "SlidingExpiration must be greater than zero.");
            }

            _slidingExpiration = value;
        }
    }

    /// <summary>
    /// Gets or sets the clock used for retention checks.
    /// </summary>
    /// <remarks>
    /// Primarily intended for testing.
    /// </remarks>
    public Func<DateTimeOffset> Clock {
        get => _clock ?? DefaultClock;
        set => _clock = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal GraphMessageListenerRetentionOptions Clone() => new() {
        MaxSeenIds = MaxSeenIds,
        SlidingExpiration = SlidingExpiration,
        Clock = Clock,
    };
}
