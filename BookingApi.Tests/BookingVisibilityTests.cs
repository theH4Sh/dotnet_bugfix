using BookingApi.Data;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingApi.Tests;

public class BookingVisibilityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly BookingService _bookingService;

    public BookingVisibilityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _bookingService = new BookingService(_db, new PaymentGateway());
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Customer_CannotRead_AnotherCustomersBooking()
    {
        var cust1 = new User { Email = "c1@test.com", Name = "Customer 1", Role = Role.Customer };
        var cust2 = new User { Email = "c2@test.com", Name = "Customer 2", Role = Role.Customer };
        _db.Users.AddRange(cust1, cust2);
        await _db.SaveChangesAsync();

        var booking = await _bookingService.CreateAsync(cust1.Id, new CreateBookingRequest("Pilgrim 1"));

        // Cust 2 attempts to view Cust 1's booking
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _bookingService.GetAsync(cust2.Id, booking.Id));

        Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Cust 1 can view their own booking
        var ownBooking = await _bookingService.GetAsync(cust1.Id, booking.Id);
        Assert.NotNull(ownBooking);
        Assert.Equal(booking.Id, ownBooking.Id);
    }

    [Fact]
    public async Task Performer_OnlyGetsToSee_PaidBookings()
    {
        var cust = new User { Email = "cust_vis@test.com", Name = "Customer", Role = Role.Customer };
        var perf = new User { Email = "perf_vis@test.com", Name = "Performer", Role = Role.Performer };
        _db.Users.AddRange(cust, perf);
        await _db.SaveChangesAsync();

        // 1. Unpaid booking (PendingPayment): performer cannot view it
        var unpaidBooking = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Unpaid Pilgrim"));
        var exUnpaid = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _bookingService.GetAsync(perf.Id, unpaidBooking.Id));
        Assert.Contains("paid", exUnpaid.Message, StringComparison.OrdinalIgnoreCase);

        // 2. Paid booking: performer CAN view it
        unpaidBooking.Status = BookingStatus.Paid;
        await _db.SaveChangesAsync();

        var viewableBooking = await _bookingService.GetAsync(perf.Id, unpaidBooking.Id);
        Assert.NotNull(viewableBooking);
        Assert.Equal(unpaidBooking.Id, viewableBooking.Id);
    }

    [Fact]
    public async Task ListAvailablePaid_OnlyReturns_PaidAndUnacceptedBookings()
    {
        var cust = new User { Email = "cust_list@test.com", Name = "Customer", Role = Role.Customer };
        var perf = new User { Email = "perf_list@test.com", Name = "Performer", Role = Role.Performer };
        _db.Users.AddRange(cust, perf);
        await _db.SaveChangesAsync();

        // Booking 1: PendingPayment (should NOT appear)
        await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Pending Booking"));

        // Booking 2: Paid (SHOULD appear)
        var paidBooking = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Paid Booking"));
        paidBooking.Status = BookingStatus.Paid;

        // Booking 3: Paid but already accepted by performer (should NOT appear in available)
        var acceptedBooking = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Accepted Booking"));
        acceptedBooking.Status = BookingStatus.Accepted;
        acceptedBooking.PerformerId = perf.Id;

        await _db.SaveChangesAsync();

        var available = await _bookingService.ListAvailablePaidAsync(page: 1, size: 10);

        Assert.Single(available);
    }
}

