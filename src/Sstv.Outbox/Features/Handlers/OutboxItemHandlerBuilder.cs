using Microsoft.Extensions.DependencyInjection;

namespace Sstv.Outbox;

/// <summary>
/// Handler builder.
/// </summary>
/// <typeparam name="TOutboxItem"></typeparam>
public sealed class OutboxItemHandlerBuilder<TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// The name of outbox item.
    /// </summary>
    public string OutboxName { get; }

    /// <summary>
    /// Type of registered handler.
    /// </summary>
    internal Type? HandlerType { get; set; }

    /// <summary>
    /// Creates new instance of <see cref="OutboxItemHandlerBuilder{TOutboxItem}"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="outboxName">The name of outbox item.</param>
    public OutboxItemHandlerBuilder(IServiceCollection services, string outboxName)
    {
        Services = services;
        OutboxName = outboxName;
    }
}
