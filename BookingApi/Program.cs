using System.Security.Claims;
using System.Text;
using BookingApi.Data;
using BookingApi.Jobs;
using BookingApi.Models;
using BookingApi.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<IFirebaseTokenVerifier, FirebaseTokenVerifier>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddSingleton<PaymentGateway>();
builder.Services.AddHostedService<ExpiryJob>();

var firebaseServiceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")
    ?? builder.Configuration["FIREBASE_SERVICE_ACCOUNT_JSON"]
    ?? builder.Configuration["Firebase:ServiceAccountJson"];

var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
    ?? builder.Configuration["FIREBASE_PROJECT_ID"]
    ?? builder.Configuration["Firebase:ProjectId"]
    ?? "dotnet-bugs";

if (FirebaseApp.DefaultInstance == null)
{
    try
    {
#pragma warning disable CS0618
        GoogleCredential? credential = null;
        if (!string.IsNullOrWhiteSpace(firebaseServiceAccountJson))
        {
            if (firebaseServiceAccountJson.TrimStart().StartsWith("{"))
                credential = GoogleCredential.FromJson(firebaseServiceAccountJson);
            else if (File.Exists(firebaseServiceAccountJson))
                credential = GoogleCredential.FromFile(firebaseServiceAccountJson);
        }

        credential ??= GoogleCredential.GetApplicationDefault();

        FirebaseApp.Create(new AppOptions
        {
            Credential = credential,
            ProjectId = firebaseProjectId
        });
#pragma warning restore CS0618
    }
    catch
    {
        // Safe fallback for testing or when credentials are not yet configured
    }
}

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = false,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

app.UseAuthentication();
app.UseAuthorization();

static long UserId(ClaimsPrincipal u) => long.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);

app.MapPost("/auth/register", async (RegisterRequest r, AuthService auth) =>
{
    try
    {
        var user = await auth.RegisterAsync(r);
        return Results.Ok(user);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/auth/login", async (LoginRequest r, AuthService auth) =>
{
    var token = await auth.LoginAsync(r);
    return token is null ? Results.Unauthorized() : Results.Ok(new { token });
});

app.MapPost("/auth/firebase", async (FirebaseLoginRequest r, AuthService auth) =>
{
    try
    {
        var token = await auth.LoginWithFirebaseAsync(r.IdToken);
        return Results.Ok(new { token });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (SecurityTokenException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

var bookings = app.MapGroup("/bookings").RequireAuthorization();

bookings.MapPost("/", async (CreateBookingRequest r, ClaimsPrincipal u, BookingService s) =>
{
    try
    {
        var booking = await s.CreateAsync(UserId(u), r);
        return Results.Ok(booking);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

bookings.MapGet("/mine", async (ClaimsPrincipal u, BookingService s, int page = 1, int size = 10) =>
    Results.Ok(await s.ListMineAsync(UserId(u), page, size)));

bookings.MapGet("/available", async (BookingService s, int page = 1, int size = 10) =>
    Results.Ok(await s.ListAvailablePaidAsync(page, size)));

bookings.MapGet("/{id:long}", async (long id, ClaimsPrincipal u, BookingService s) =>
{
    try
    {
        var booking = await s.GetAsync(UserId(u), id);
        return booking is null ? Results.NotFound() : Results.Ok(booking);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
});

bookings.MapPost("/{id:long}/pay", (long id, ClaimsPrincipal u, BookingService s) =>
    Results.Ok(s.Pay(UserId(u), id)));

bookings.MapPost("/{id:long}/cancel", async (long id, ClaimsPrincipal u, BookingService s) =>
{
    try
    {
        var booking = await s.CancelAsync(UserId(u), id);
        return Results.Ok(booking);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

bookings.MapPost("/{id:long}/accept", async (long id, ClaimsPrincipal u, BookingService s) =>
{
    try
    {
        var booking = await s.AcceptAsync(UserId(u), id);
        return Results.Ok(booking);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public partial class Program { }

