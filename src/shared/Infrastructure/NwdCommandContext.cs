using System;
using System.Collections.Generic;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class NwdCommandContext
{
    public bool ReadOnly { get; init; }
    public bool EnableSendCode { get; init; }
    public int NavisworksYear { get; init; }
    public string? TargetId { get; init; }
    public IReadOnlyDictionary<string, INwdCommand>? Commands { get; init; }

    /// <summary>
    /// When the caller stops waiting (its timeout, less a margin for the reply). Long walks check
    /// <see cref="PastDeadline"/> and return what they have, so Navisworks is not kept busy for nobody.
    /// </summary>
    public DateTime DeadlineUtc { get; init; } = DateTime.MaxValue;

    public bool PastDeadline => DateTime.UtcNow >= DeadlineUtc;

    /// <summary>Caller timeout minus a reply margin, counted from when the request was read.</summary>
    public static DateTime DeadlineFor(DateTime receivedUtc, int timeoutMs)
        => receivedUtc.AddMilliseconds(Math.Max(1000, timeoutMs - 2000));
}
