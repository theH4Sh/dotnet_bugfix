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
    Results.Ok(await auth.RegisterAsync(r)));

app.MapPost("/auth/login", async (LoginRequest r, AuthService auth) =>
{
    var token = await auth.LoginAsync(r);
    return token is null ? Results.Unauthorized() : Results.Ok(new { token });
});

var bookings = app.MapGroup("/bookings").RequireAuthorization();

bookings.MapPost("/", async (CreateBookingRequest r, ClaimsPrincipal u, BookingService s) =>
    Results.Ok(await s.CreateAsync(UserId(u), r)));

bookings.MapGet("/mine", (ClaimsPrincipal u, BookingService s, int page = 1, int size = 10) =>
    Results.Ok(s.ListMine(UserId(u), page, size)));

bookings.MapGet("/{id:long}", async (long id, BookingService s) =>
{
    var booking = await s.GetAsync(id);
    return booking is null ? Results.NotFound() : Results.Ok(booking);
});

bookings.MapPost("/{id:long}/pay", (long id, ClaimsPrincipal u, BookingService s) =>
    Results.Ok(s.Pay(UserId(u), id)));

bookings.MapPost("/{id:long}/cancel", async (long id, ClaimsPrincipal u, BookingService s) =>
    Results.Ok(await s.CancelAsync(UserId(u), id)));

bookings.MapPost("/{id:long}/accept", async (long id, ClaimsPrincipal u, BookingService s) =>
    Results.Ok(await s.AcceptAsync(UserId(u), id)));

app.Run();
