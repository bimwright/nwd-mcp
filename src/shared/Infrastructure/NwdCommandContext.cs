using System;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class NwdCommandContext
{
    public bool ReadOnly { get; init; }
    public bool EnableSendCode { get; init; }
    public int NavisworksYear { get; init; }
    public string? TargetId { get; init; }
}
