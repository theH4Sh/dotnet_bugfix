using BookingApi.Data;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingApi.Tests;

public class BookingPriceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly BookingService _bookingService;

    public BookingPriceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var paymentGateway = new PaymentGateway();
        _bookingService = new BookingService(_db, paymentGateway);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task CreateBooking_AlwaysEnforcesFixedPriceOf500_EvenIfClientSendsDifferentAmount()
    {
        var customer = new User { Email = "cust@example.com", Name = "Customer", Role = Role.Customer };
        _db.Users.Add(customer);
        await _db.SaveChangesAsync();

        // Client attempts to pass 10 SAR instead of 500
        var request = new CreateBookingRequest("John Doe", 10m);
        var booking = await _bookingService.CreateAsync(customer.Id, request);

        Assert.Equal(500m, booking.Amount);

        // Client attempts to pass 0 SAR
        var requestZero = new CreateBookingRequest("Jane Doe", 0m);
        var bookingZero = await _bookingService.CreateAsync(customer.Id, requestZero);

        Assert.Equal(500m, bookingZero.Amount);

        // Verify stored in database as 500
        var fromDb = await _db.Bookings.FindAsync(booking.Id);
        Assert.NotNull(fromDb);
        Assert.Equal(500m, fromDb.Amount);
    }

    [Fact]
    public async Task CreateBooking_WithoutAmount_DefaultsTo500()
    {
        var customer = new User { Email = "cust2@example.com", Name = "Customer 2", Role = Role.Customer };
        _db.Users.Add(customer);
        await _db.SaveChangesAsync();

        var request = new CreateBookingRequest("Pilgrim Person");
        var booking = await _bookingService.CreateAsync(customer.Id, request);

        Assert.Equal(500m, booking.Amount);
    }

    [Fact]
    public async Task CreateBooking_WithoutPilgrimName_ThrowsArgumentException()
    {
        var request = new CreateBookingRequest("   ");
        await Assert.ThrowsAsync<ArgumentException>(() => _bookingService.CreateAsync(1, request));
    }
}

