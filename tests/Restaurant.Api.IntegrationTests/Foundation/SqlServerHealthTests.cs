using Microsoft.AspNetCore.Mvc.Testing;
using Restaurant.Api.IntegrationTests.Infrastructure;
using System.Net;

namespace Restaurant.Api.IntegrationTests.Foundation;

public sealed class SqlServerReadinessTests : IClassFixture<RestaurantApiFactory>
{
    private readonly RestaurantApiFactory _factory;

    public SqlServerReadinessTests(RestaurantApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ReadinessReturnsOkWhileSqlServerIsAvailable()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

// Own factory/container: stopping SQL cannot affect the success or migration tests.
public sealed class SqlServerOutageTests : IClassFixture<RestaurantApiFactory>
{
    private readonly RestaurantApiFactory _factory;

    public SqlServerOutageTests(RestaurantApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ReadinessReturns503AfterSqlServerStopsWhileLivenessRemainsOk()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var before = await client.GetAsync("/health/ready", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await _factory.StopSqlServerAsync(timeout.Token);

        using var ready = await client.GetAsync("/health/ready", timeout.Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync(timeout.Token));

        using var live = await client.GetAsync("/health/live", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync(timeout.Token));
    }
}
