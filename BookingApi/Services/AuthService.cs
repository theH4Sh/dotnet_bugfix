using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BookingApi.Data;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BookingApi.Services;

public class AuthService(AppDbContext db, IConfiguration config)
{
    public async Task<UserResponse> RegisterAsync(RegisterRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Email))
            throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(r.Password))
            throw new ArgumentException("Password is required.");

        var normalizedEmail = r.Email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (exists)
            throw new InvalidOperationException("Email is already registered.");

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(r.Password);
        var user = new User
        {
            Email = normalizedEmail,
            Password = hashedPassword,
            Name = r.Name?.Trim() ?? "",
            Role = r.Role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new UserResponse(user.Id, user.Email, user.Name, user.Role);
    }

    public async Task<string?> LoginAsync(LoginRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password))
            return null;

        var normalizedEmail = r.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        if (user is null) return null;

        bool isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(r.Password, user.Password);
        }
        catch
        {
            // Fallback for legacy plaintext passwords, if any
            if (user.Password == r.Password)
            {
                isValid = true;
                user.Password = BCrypt.Net.BCrypt.HashPassword(r.Password);
                await db.SaveChangesAsync();
            }
        }

        if (!isValid) return null;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddDays(365), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
