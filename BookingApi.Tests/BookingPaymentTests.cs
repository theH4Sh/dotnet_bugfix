using BookingApi.Data;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingApi.Tests;

public class BookingPaymentTests : IDisposable
{
    private readonly string _connectionString = $"Data Source=booking-payment-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _anchorConnection;
    private readonly AppDbContext _db;

    public BookingPaymentTests()
    {
        _anchorConnection = new SqliteConnection(_connectionString);
        _anchorConnection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _anchorConnection.Dispose();
    }

    [Fact]
    public async Task ConcurrentPaymentRequests_OnlyOnePaymentIsCharged()
    {
        var customer = new User { Email = "payment-customer@example.com", Name = "Customer", Role = Role.Customer };
        _db.Users.Add(customer);
        await _db.SaveChangesAsync();

        var bookingService = new BookingService(_db, new PaymentGateway());
        var booking = await bookingService.CreateAsync(customer.Id, new CreateBookingRequest("Pilgrim"));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        await using var firstDb = new AppDbContext(options);
        await using var secondDb = new AppDbContext(options);

        var firstService = new BookingService(firstDb, new PaymentGateway());
        var secondService = new BookingService(secondDb, new PaymentGateway());
        var results = await Task.WhenAll(
            TryPayAsync(firstService, customer.Id, booking.Id),
            TryPayAsync(secondService, customer.Id, booking.Id));

        Assert.Single(results, paid => paid);

        await using var verificationDb = new AppDbContext(options);
        Assert.Equal(1, await verificationDb.Payments.CountAsync(p => p.BookingId == booking.Id));
        Assert.Equal(
            BookingStatus.Paid,
            (await verificationDb.Bookings.AsNoTracking().SingleAsync(b => b.Id == booking.Id)).Status);
    }

    private static async Task<bool> TryPayAsync(BookingService service, long userId, long bookingId)
    {
        try
        {
            await service.PayAsync(userId, bookingId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
