using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookingApi.Data;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BookingApi.Tests;

public class MockFirebaseVerifier : IFirebaseTokenVerifier
{
    private readonly Dictionary<string, FirebaseTokenResult> _tokens = new();

    public void RegisterToken(string token, FirebaseTokenResult result)
    {
        _tokens[token] = result;
    }

    public Task<FirebaseTokenResult> VerifyTokenAsync(string idToken, CancellationToken ct = default)
    {
        if (_tokens.TryGetValue(idToken, out var result))
        {
            return Task.FromResult(result);
        }

        throw new SecurityTokenException("Invalid token signature or expired.");
    }
}

public class FirebaseAuthTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly MockFirebaseVerifier _mockVerifier;
    private readonly AuthService _authService;

    public FirebaseAuthTests()
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

        _mockVerifier = new MockFirebaseVerifier();
        _authService = new AuthService(_db, _config, _mockVerifier);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task FirebaseLogin_CreatesNewCustomer_WhenUserDoesNotExist()
    {
        _mockVerifier.RegisterToken("new-user-token", new FirebaseTokenResult(
            Uid: "fb-uid-1",
            Email: "newcustomer@example.com",
            EmailVerified: true,
            Name: "New Customer"
        ));

        var jwtToken = await _authService.LoginWithFirebaseAsync("new-user-token");

        Assert.NotNull(jwtToken);
        Assert.NotEmpty(jwtToken);

        // Verify user was created in DB
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == "newcustomer@example.com");
        Assert.NotNull(user);
        Assert.Equal(Role.Customer, user.Role);
        Assert.Equal("New Customer", user.Name);

        // Verify JWT claims
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwtToken);
        Assert.Equal(user.Id.ToString(), token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(Role.Customer.ToString(), token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task FirebaseLogin_LinksToExistingCustomer_WhenEmailIsVerified()
    {
        var existingCustomer = new User
        {
            Email = "verified@example.com",
            Name = "Existing Customer",
            Role = Role.Customer,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!")
        };
        _db.Users.Add(existingCustomer);
        await _db.SaveChangesAsync();

        _mockVerifier.RegisterToken("verified-token", new FirebaseTokenResult(
            Uid: "fb-uid-2",
            Email: "verified@example.com",
            EmailVerified: true,
            Name: "Updated Name"
        ));

        var jwtToken = await _authService.LoginWithFirebaseAsync("verified-token");

        Assert.NotNull(jwtToken);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwtToken);
        Assert.Equal(existingCustomer.Id.ToString(), token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(Role.Customer.ToString(), token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task FirebaseLogin_RejectsLinking_WhenEmailIsUnverified()
    {
        var existingCustomer = new User
        {
            Email = "unverified_target@example.com",
            Name = "Victim Customer",
            Role = Role.Customer,
            Password = BCrypt.Net.BCrypt.HashPassword("VictimPass123!")
        };
        _db.Users.Add(existingCustomer);
        await _db.SaveChangesAsync();

        _mockVerifier.RegisterToken("unverified-token", new FirebaseTokenResult(
            Uid: "fb-uid-attacker",
            Email: "unverified_target@example.com",
            EmailVerified: false,
            Name: "Attacker"
        ));

        var ex = await Assert.ThrowsAsync<SecurityTokenException>(() =>
            _authService.LoginWithFirebaseAsync("unverified-token"));

        Assert.Contains("verified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FirebaseLogin_NeverTakesOver_PerformerAccount()
    {
        var existingPerformer = new User
        {
            Email = "performer@example.com",
            Name = "Legit Performer",
            Role = Role.Performer,
            Password = BCrypt.Net.BCrypt.HashPassword("PerformerSecret123!")
        };
        _db.Users.Add(existingPerformer);
        await _db.SaveChangesAsync();

        _mockVerifier.RegisterToken("performer-takeover-token", new FirebaseTokenResult(
            Uid: "fb-uid-hijack",
            Email: "performer@example.com",
            EmailVerified: true,
            Name: "Hijacker"
        ));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _authService.LoginWithFirebaseAsync("performer-takeover-token"));

        Assert.Contains("Performer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FirebaseLogin_RejectsInvalidToken()
    {
        var ex = await Assert.ThrowsAsync<SecurityTokenException>(() =>
            _authService.LoginWithFirebaseAsync("invalid-random-token"));

        Assert.Contains("Invalid Firebase ID token", ex.Message);
    }
}

