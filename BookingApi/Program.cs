using System.Security.Claims;
using System.Text;
using BookingApi.Data;
using BookingApi.Jobs;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddSingleton<PaymentGateway>();
builder.Services.AddHostedService<ExpiryJob>();

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

