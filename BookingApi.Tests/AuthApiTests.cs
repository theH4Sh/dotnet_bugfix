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

public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;

    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

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
    public async Task PostRegister_Success_Returns200_WithoutPasswordInResponse()
    {
        var client = _factory.CreateClient();
        var registerPayload = new RegisterRequest("api_user@example.com", "SecurePassword123!", "API User", Role.Customer);

        var response = await client.PostAsJsonAsync("/auth/register", registerPayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", responseString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurePassword123!", responseString);

        using var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;
        Assert.Equal("api_user@example.com", root.GetProperty("email").GetString());
        Assert.Equal("API User", root.GetProperty("name").GetString());
        Assert.Equal((int)Role.Customer, root.GetProperty("role").GetInt32());
        Assert.True(root.TryGetProperty("id", out _));
        Assert.False(root.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task PostRegister_DuplicateEmail_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        var user1 = new RegisterRequest("same_email@example.com", "Password1!", "User One", Role.Customer);
        var user2 = new RegisterRequest("same_email@example.com", "Password2!", "User Two", Role.Customer);

        var response1 = await client.PostAsJsonAsync("/auth/register", user1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var response2 = await client.PostAsJsonAsync("/auth/register", user2);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task PostLogin_WithValidPassword_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var registerPayload = new RegisterRequest("login_test@example.com", "CorrectPassword123!", "Login User", Role.Customer);
        await client.PostAsJsonAsync("/auth/register", registerPayload);

        var loginPayload = new LoginRequest("login_test@example.com", "CorrectPassword123!");
        var response = await client.PostAsJsonAsync("/auth/login", loginPayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", responseBody);
    }

    [Fact]
    public async Task PostLogin_WithInvalidPassword_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var registerPayload = new RegisterRequest("login_fail@example.com", "CorrectPassword123!", "Login User", Role.Customer);
        await client.PostAsJsonAsync("/auth/register", registerPayload);

        var loginPayload = new LoginRequest("login_fail@example.com", "WrongPassword!");
        var response = await client.PostAsJsonAsync("/auth/login", loginPayload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

