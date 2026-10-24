using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingApi.Data;
using BookingApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingApi.Tests;

public class HealthApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;

    public HealthApiTests(WebApplicationFactory<Program> factory)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    service => service.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

                using var scope = services.BuildServiceProvider().CreateScope();
                scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task GetHealth_ReturnsHealthyWhenDatabaseIsReachable()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new User { Email = "health-customer@example.com", Role = Role.Customer };
        db.Users.Add(customer);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        db.Bookings.AddRange(Enumerable.Range(1, 6).Select(index => new Booking
        {
            CustomerId = customer.Id,
            PilgrimName = $"Private name {index}",
            Amount = 500m,
            Status = BookingStatus.Paid,
            CreatedAt = now.AddMinutes(index)
        }));
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal("connected", body.GetProperty("database").GetString());

        var bookings = body.GetProperty("recentBookings").EnumerateArray().ToArray();
        Assert.Equal(5, bookings.Length);
        Assert.All(bookings, booking =>
        {
            Assert.True(booking.TryGetProperty("status", out _));
            Assert.True(booking.TryGetProperty("createdAt", out _));
            Assert.False(booking.TryGetProperty("pilgrimName", out _));
            Assert.False(booking.TryGetProperty("amount", out _));
            Assert.False(booking.TryGetProperty("customerId", out _));
        });

        var timestamps = bookings
            .Select(booking => booking.GetProperty("createdAt").GetDateTimeOffset())
            .ToArray();
        Assert.Equal(timestamps.OrderDescending(), timestamps);
    }
}
