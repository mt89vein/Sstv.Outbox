using System.Diagnostics;

namespace Sstv.Outbox;

/// <summary>
/// Activity sources.
/// </summary>
public static class ActivitySources
{
    /// <summary>
    /// Source name.
    /// </summary>
    public const string OutboxSource = "Sstv.Outbox";

    /// <summary>
    /// Tracing activity source.
    /// </summary>
    public static readonly ActivitySource Tracing = new(OutboxSource);
}
