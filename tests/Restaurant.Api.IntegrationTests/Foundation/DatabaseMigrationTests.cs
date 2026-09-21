using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Api.IntegrationTests.Infrastructure;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.IntegrationTests.Foundation;

public sealed class DatabaseMigrationTests : IClassFixture<RestaurantApiFactory>
{
    private readonly RestaurantApiFactory _factory;

    public DatabaseMigrationTests(RestaurantApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MigrationsApplyToANewDatabaseAndLeaveNoPendingMigrations()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var connectionString = new SqlConnectionStringBuilder(_factory.ConnectionString)
        {
            InitialCatalog = $"MigrationTest_{Guid.NewGuid():N}"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlServer(connectionString).Options;
        await using var dbContext = new RestaurantDbContext(options);

        Assert.False(await dbContext.GetService<IRelationalDatabaseCreator>().ExistsAsync(timeout.Token));
        var migrations = dbContext.Database.GetMigrations().ToArray();
        Assert.NotEmpty(migrations);
        Assert.Contains(migrations, migration => migration.EndsWith("_InitialFoundation", StringComparison.Ordinal));

        await dbContext.Database.MigrateAsync(timeout.Token);

        Assert.True(await dbContext.Database.CanConnectAsync(timeout.Token));
        Assert.Equal(migrations, (await dbContext.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray());
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync(timeout.Token));
        Assert.False(dbContext.Database.HasPendingModelChanges());

        // A second controlled application must not add duplicate history entries.
        await dbContext.Database.MigrateAsync(timeout.Token);
        Assert.Equal(migrations, (await dbContext.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray());
        // The factory disposes the entire server, including this isolated database.
    }
}
