using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Restaurant.Api.IntegrationTests.Infrastructure;

public sealed class RestaurantApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .WithPassword($"OrderFlow!{Guid.NewGuid():N}")
        .Build();
    private bool _disposed;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await _sqlServer.StartAsync(timeout.Token);
            ConnectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
            {
                InitialCatalog = "RestaurantOrderingTests",
                ConnectTimeout = 2,
                ConnectRetryCount = 0,
                Pooling = false
            }.ConnectionString;

            // Test setup only: production startup never applies migrations.
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
            await dbContext.Database.MigrateAsync(timeout.Token);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new InvalidOperationException("Initialize the SQL Server fixture before creating the API host.");
        }

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
    }

    public Task StopSqlServerAsync(CancellationToken cancellationToken) =>
        _sqlServer.StopAsync(cancellationToken);

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await _sqlServer.DisposeAsync();
        }
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
