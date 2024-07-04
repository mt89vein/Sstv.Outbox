using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Sstv.Outbox;

/// <summary>
/// Outbox maintenance API.
/// </summary>
public static class OutboxApi
{
    /// <summary>
    /// Maps maintenance API for <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="webApplication">WebApp.</param>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    public static RouteGroupBuilder MapOutboxMaintenanceEndpoints<TOutboxItem>(this WebApplication webApplication)
        where TOutboxItem : class, IOutboxItem
    {
        var name = typeof(TOutboxItem).Name;
        var group = webApplication
            .MapGroup($"/{name}")
            .WithTags(name);

        group.MapGet("/search", async (
                [FromServices] IOutboxMaintenanceRepository<TOutboxItem> repository,
                int skip = 0,
                int take = 100,
                CancellationToken ct = default
            ) => Results.Ok(await repository.GetChunkAsync(skip, take, ct))
        ).Produces(200, typeof(IEnumerable<TOutboxItem>));

        group.MapDelete("/clear", async (
            [FromServices] IOutboxMaintenanceRepository<TOutboxItem> repository,
            CancellationToken ct = default
        ) =>
        {
            await repository.ClearAsync(ct);
            return Results.Ok();
        }).Produces(200);

        group.MapDelete("/delete", async (
            [FromServices] IOutboxMaintenanceRepository<TOutboxItem> repository,
            [FromBody] Guid[] ids,
            CancellationToken ct = default
        ) =>
        {
            await repository.DeleteAsync(ids, ct);
            return Results.Ok();
        }).Produces(200);

        group.MapPost("/restart", async (
            [FromServices] IOutboxMaintenanceRepository<TOutboxItem> repository,
            [FromBody] Guid[] ids,
            CancellationToken ct = default
        ) =>
        {
            await repository.RestartAsync(ids, ct);
            return Results.Ok();
        }).Produces(200);

        return group;
    }
}