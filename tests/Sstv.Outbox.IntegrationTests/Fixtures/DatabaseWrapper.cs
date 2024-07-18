using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Sstv.Outbox.IntegrationTests.Fixtures;

/// <summary>
/// Singleton object, that used to init database and clear before the tests.
/// </summary>
public sealed class DatabaseWrapper : IAsyncDisposable, IDisposable
{
    private Respawner? _respawner;
    private NpgsqlConnection? _connection;

    /// <summary>
    /// Init cleaner.
    /// </summary>
    public async Task InitAsync(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_respawner != null)
        {
            return;
        }

        await context.Database.MigrateAsync();
        await context.Database.EnsureCreatedAsync();

        _connection = new NpgsqlConnection(SetupContainer.ConnectionString);
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = [context.Model.GetDefaultSchema() ?? "public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    /// <summary>
    /// Clean database.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_respawner == null || _connection == null)
        {
            return;
        }
        await _respawner.ResetAsync(_connection);
    }

    /// <summary>
    /// Clean resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Clean resources.
    /// </summary>
    public void Dispose()
    {
        _connection?.Dispose();
    }
}
