using DotNet.Testcontainers.Builders;
using Sstv.Outbox.IntegrationTests.Fixtures;
using Testcontainers.PostgreSql;

/// <summary>
/// Инициализация контейнера
/// </summary>
[SetUpFixture]
#pragma warning disable CA1050
public static class SetupContainer
#pragma warning restore CA1050
{
    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("outbox_integration_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithWaitStrategy(Wait
            .ForUnixContainer()
            .UntilCommandIsCompleted("pg_isready"))
        .WithCleanUp(true)
        .Build();

    /// <summary>
    /// Строка подключения к БД.
    /// </summary>
    public static string ConnectionString => Container.GetConnectionString();

    /// <summary>
    /// Объект для инициализации respawner и вызова очистки БД.
    /// </summary>
    public static DatabaseWrapper Database { get; } = new();

    /// <summary>
    /// Инициализация контейнера.
    /// </summary>
    [OneTimeSetUp]
    public static async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    /// <summary>
    /// Освобождение ресурсов.
    /// </summary>
    [OneTimeTearDown]
    public static async Task TearDownAsync()
    {
        await Container.DisposeAsync();
        await Database.DisposeAsync();
    }
}
