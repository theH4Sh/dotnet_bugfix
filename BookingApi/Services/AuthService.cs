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
    public async Task<User> RegisterAsync(RegisterRequest r)
    {
        var user = new User { Email = r.Email, Password = r.Password, Name = r.Name, Role = r.Role };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<string?> LoginAsync(LoginRequest r)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == r.Email && u.Password == r.Password);
        if (user is null) return null;

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
