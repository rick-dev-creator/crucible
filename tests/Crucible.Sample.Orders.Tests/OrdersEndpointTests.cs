using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Crucible.Sample.Orders.Tests;

public sealed class OrdersEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public OrdersEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostOrders_HappyPath_Returns200()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/orders?carrier=UPS&priorityDays=2",
            new { customerId = "C-001", amount = 100.0m, currency = "USD" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostOrders_DomainFailure_Returns400()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/orders?carrier=UPS&priorityDays=2",
            new { customerId = "", amount = 100.0m, currency = "USD" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostOrders_BannedCustomer_Returns400()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/orders?carrier=UPS&priorityDays=2",
            new { customerId = "BANNED-007", amount = 100.0m, currency = "USD" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
