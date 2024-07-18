using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Sstv.Outbox.Sample;

namespace Sstv.Outbox.IntegrationTests.Fixtures;

/// <summary>
/// Фабрика веб приложения.
/// </summary>
public class WebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Конструктор типа.
    /// </summary>
    static WebAppFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");

        // отключаем перезагрузку конфигурации во время тестирования,
        // чтобы избежать ошибки из-за его лимита в 128 нотифаеров
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
    }

    /// <summary>
    /// Функция для переопределения служб в DI контейнере.
    /// </summary>
    private readonly Action<IServiceCollection>? _configureServices;

    /// <summary>
    /// Источник подключений к контейнеру с PostgreSQL
    /// </summary>
    private static NpgsqlDataSource? _npgsqlDataSource;

    /// <summary>
    /// Создает новый экземпляр класса <see cref="WebAppFactory"/>.
    /// </summary>
    /// <param name="configureServices">Функция для переопределения служб в DI контейнере.</param>
    public WebAppFactory(Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
    }

    /// <summary>
    /// Настройка веб хоста.
    /// </summary>
    /// <param name="builder">Билдер веб хоста.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        var configOverrides = new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = SetupContainer.ConnectionString,
            ["Outbox:MyOutboxItem:IsWorkerEnabled"] = bool.FalseString,
            ["Outbox:OneMoreOutboxItem:IsWorkerEnabled"] = bool.FalseString,
            ["Outbox:StrictOutboxItem:IsWorkerEnabled"] = bool.FalseString,
            ["Outbox:EfOutboxItem:IsWorkerEnabled"] = bool.FalseString,
            ["Outbox:KafkaEfOutboxItem:IsWorkerEnabled"] = bool.FalseString,
            ["Outbox:KafkaNpgsqlOutboxItem:IsWorkerEnabled"] = bool.FalseString,
            ["Outbox:KafkaEfOutboxItemWithPriority:IsWorkerEnabled"] = bool.FalseString,
            ["Outbox:KafkaNpgsqlOutboxItemWithPriority:IsWorkerEnabled"] = bool.FalseString,
        };

        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(configOverrides!);
        });

        _npgsqlDataSource ??= new NpgsqlDataSourceBuilder(SetupContainer.ConnectionString) { Name = "Sstv" }
            .EnableDynamicJson()
            .BuildMultiHost()
            .WithTargetSession(TargetSessionAttributes.Primary);

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(typeof(NpgsqlDataSource), _npgsqlDataSource));

            using var scope = services
                .BuildServiceProvider()
                .CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            SetupContainer.Database.InitAsync(context).GetAwaiter().GetResult();

            _configureServices?.Invoke(services);
        });
    }
}
