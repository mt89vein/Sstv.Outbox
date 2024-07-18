namespace Sstv.Outbox;

/// <summary>
/// Processing priority of outbox items.
/// </summary>
/// <param name="Enabled">Enabled or not.</param>
public sealed record PriorityFeature(bool Enabled);
