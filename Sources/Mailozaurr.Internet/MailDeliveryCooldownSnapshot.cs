using System;

namespace Mailozaurr;

/// <summary>An immutable view of one mail-delivery cooldown's state.</summary>
public sealed class MailDeliveryCooldownSnapshot {
    /// <summary>Gets the consecutive failure count since the last successful attempt or cooldown expiry.</summary>
    public int FailureCount { get; }

    /// <summary>Gets the UTC expiry of the active or most recently elapsed cooldown, or null when no cooldown is recorded.</summary>
    public DateTimeOffset? CooldownUntilUtc { get; }

    internal MailDeliveryCooldownSnapshot(int failureCount, DateTimeOffset? cooldownUntilUtc) {
        FailureCount = failureCount;
        CooldownUntilUtc = cooldownUntilUtc;
    }
}
