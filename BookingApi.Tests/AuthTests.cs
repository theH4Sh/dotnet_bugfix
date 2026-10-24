using System.Text.Json;
using BookingApi.Data;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BookingApi.Tests;

public class AuthTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly AuthService _authService;

    public AuthTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Key", "SuperSecretKeyThatIsAtLeast32BytesLongForHmacSha256!"}
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _authService = new AuthService(_db, _config);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Register_HashesPassword_AndDoesNotReturnPassword()
    {
        var request = new RegisterRequest("test@example.com", "MySecretPassword123", "Test User", Role.Customer);

        var result = await _authService.RegisterAsync(request);

        // Result DTO check
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Test User", result.Name);
        Assert.Equal(Role.Customer, result.Role);

        // Verify JSON serialization of UserResponse does not contain password
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);

        // Verify stored user in database has hashed password using BCrypt
        var dbUser = await _db.Users.SingleAsync(u => u.Email == "test@example.com");
        Assert.NotEqual("MySecretPassword123", dbUser.Password);
        Assert.True(BCrypt.Net.BCrypt.Verify("MySecretPassword123", dbUser.Password));

        // Verify User entity serialization ignores Password
        var userJson = JsonSerializer.Serialize(dbUser);
        Assert.DoesNotContain("MySecretPassword123", userJson);
        Assert.DoesNotContain("password", userJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        var request1 = new RegisterRequest("duplicate@example.com", "Password123", "First", Role.Customer);
        await _authService.RegisterAsync(request1);

        var request2 = new RegisterRequest("duplicate@example.com", "Password456", "Second", Role.Customer);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.RegisterAsync(request2));
        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Case-insensitive duplicate check
        var request3 = new RegisterRequest("DUPLICATE@EXAMPLE.COM", "Password789", "Third", Role.Customer);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.RegisterAsync(request3));
        Assert.Contains("already registered", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsValidJwt()
    {
        var registerRequest = new RegisterRequest("login@example.com", "CorrectPassword!", "Login User", Role.Customer);
        await _authService.RegisterAsync(registerRequest);

        var token = await _authService.LoginAsync(new LoginRequest("login@example.com", "CorrectPassword!"));

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ReturnsNull()
    {
        var registerRequest = new RegisterRequest("login2@example.com", "CorrectPassword!", "Login User", Role.Customer);
        await _authService.RegisterAsync(registerRequest);

        var token = await _authService.LoginAsync(new LoginRequest("login2@example.com", "WrongPassword!"));

        Assert.Null(token);
    }
}

