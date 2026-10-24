using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingApi.Data;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingApi.Tests;

public class FirebaseAuthApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;
    private readonly MockFirebaseVerifier _mockVerifier;

    public FirebaseAuthApiTests(WebApplicationFactory<Program> factory)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _mockVerifier = new MockFirebaseVerifier();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

                var verifierDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFirebaseTokenVerifier));
                if (verifierDescriptor != null) services.Remove(verifierDescriptor);

                services.AddSingleton<IFirebaseTokenVerifier>(_mockVerifier);

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task PostAuthFirebase_NewUser_Returns200_WithToken()
    {
        _mockVerifier.RegisterToken("api-new-token", new FirebaseTokenResult(
            Uid: "api-uid-1",
            Email: "api_new@test.com",
            EmailVerified: true,
            Name: "API New Customer"
        ));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/firebase", new FirebaseLoginRequest("api-new-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("token", out var tokenProp));
        Assert.NotEmpty(tokenProp.GetString()!);
    }

    [Fact]
    public async Task PostAuthFirebase_PerformerAccount_Returns403Forbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User
        {
            Email = "api_perf@test.com",
            Name = "API Performer",
            Role = Role.Performer,
            Password = BCrypt.Net.BCrypt.HashPassword("Pass123!")
        });
        await db.SaveChangesAsync();

        _mockVerifier.RegisterToken("api-perf-token", new FirebaseTokenResult(
            Uid: "api-uid-perf",
            Email: "api_perf@test.com",
            EmailVerified: true,
            Name: "API Performer"
        ));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/firebase", new FirebaseLoginRequest("api-perf-token"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthFirebase_UnverifiedExistingCustomer_Returns401Unauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User
        {
            Email = "api_unverified@test.com",
            Name = "Existing Verified Customer",
            Role = Role.Customer,
            Password = BCrypt.Net.BCrypt.HashPassword("Pass123!")
        });
        await db.SaveChangesAsync();

        _mockVerifier.RegisterToken("api-unverified-token", new FirebaseTokenResult(
            Uid: "api-uid-attacker",
            Email: "api_unverified@test.com",
            EmailVerified: false,
            Name: "Attacker"
        ));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/firebase", new FirebaseLoginRequest("api-unverified-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthFirebase_InvalidToken_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/firebase", new FirebaseLoginRequest("totally-fake-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

