using BookingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>().HasIndex(u => u.Email).IsUnique();
        m.Entity<Booking>().HasOne(b => b.Customer).WithMany().HasForeignKey(b => b.CustomerId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Booking>().HasOne(b => b.Performer).WithMany().HasForeignKey(b => b.PerformerId).OnDelete(DeleteBehavior.Restrict);
    }
}
