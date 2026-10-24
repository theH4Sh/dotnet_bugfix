using BookingApi.Data;
using BookingApi.Models;
using BookingApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingApi.Tests;

public class BookingAcceptAndCancelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly BookingService _bookingService;

    public BookingAcceptAndCancelTests()
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
    public async Task Customer_CannotCancel_AnotherCustomersBooking()
    {
        var cust1 = new User { Email = "cust1@example.com", Name = "Cust 1", Role = Role.Customer };
        var cust2 = new User { Email = "cust2@example.com", Name = "Cust 2", Role = Role.Customer };
        _db.Users.AddRange(cust1, cust2);
        await _db.SaveChangesAsync();

        var booking = await _bookingService.CreateAsync(cust1.Id, new CreateBookingRequest("Pilgrim A"));

        // Cust 2 attempts to cancel Cust 1's booking
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _bookingService.CancelAsync(cust2.Id, booking.Id));

        Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Booking status remains unchanged
        var fromDb = await _db.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.PendingPayment, fromDb!.Status);
    }

    [Fact]
    public async Task Customer_CanCancel_OwnBooking_BeforePerformerAccepts()
    {
        var cust = new User { Email = "cust_cancel@example.com", Name = "Cust", Role = Role.Customer };
        _db.Users.Add(cust);
        await _db.SaveChangesAsync();

        // Cancel while PendingPayment
        var booking1 = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Pilgrim 1"));
        var cancelled1 = await _bookingService.CancelAsync(cust.Id, booking1.Id);
        Assert.Equal(BookingStatus.Cancelled, cancelled1.Status);

        // Cancel while Paid (before performer acceptance)
        var booking2 = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Pilgrim 2"));
        booking2.Status = BookingStatus.Paid;
        await _db.SaveChangesAsync();

        var cancelled2 = await _bookingService.CancelAsync(cust.Id, booking2.Id);
        Assert.Equal(BookingStatus.Cancelled, cancelled2.Status);
    }

    [Fact]
    public async Task Customer_CannotCancel_AfterPerformerAccepts()
    {
        var cust = new User { Email = "cust_acc@example.com", Name = "Cust", Role = Role.Customer };
        var perf = new User { Email = "perf_acc@example.com", Name = "Perf", Role = Role.Performer };
        _db.Users.AddRange(cust, perf);
        await _db.SaveChangesAsync();

        var booking = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Pilgrim Accepted"));
        booking.Status = BookingStatus.Paid;
        await _db.SaveChangesAsync();

        // Performer accepts the booking
        await _bookingService.AcceptAsync(perf.Id, booking.Id);

        // Customer attempts to cancel now
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bookingService.CancelAsync(cust.Id, booking.Id));

        Assert.Contains("accepted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customer_CannotAccept_Booking()
    {
        var cust1 = new User { Email = "cust_not_perf1@example.com", Name = "Cust 1", Role = Role.Customer };
        var cust2 = new User { Email = "cust_not_perf2@example.com", Name = "Cust 2", Role = Role.Customer };
        _db.Users.AddRange(cust1, cust2);
        await _db.SaveChangesAsync();

        var booking = await _bookingService.CreateAsync(cust1.Id, new CreateBookingRequest("Pilgrim X"));
        booking.Status = BookingStatus.Paid;
        await _db.SaveChangesAsync();

        // Customer 2 tries to accept
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _bookingService.AcceptAsync(cust2.Id, booking.Id));

        Assert.Contains("performers", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Performer_CannotAccept_OwnBooking()
    {
        var performerUser = new User { Email = "dual_role@example.com", Name = "Dual", Role = Role.Performer };
        _db.Users.Add(performerUser);
        await _db.SaveChangesAsync();

        var booking = await _bookingService.CreateAsync(performerUser.Id, new CreateBookingRequest("Pilgrim Self"));
        booking.Status = BookingStatus.Paid;
        await _db.SaveChangesAsync();

        // Performer attempts to accept a booking they created
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bookingService.AcceptAsync(performerUser.Id, booking.Id));

        Assert.Contains("own booking", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Performer_CannotAccept_UnpaidBooking()
    {
        var cust = new User { Email = "unpaid_cust@example.com", Name = "Cust", Role = Role.Customer };
        var perf = new User { Email = "unpaid_perf@example.com", Name = "Perf", Role = Role.Performer };
        _db.Users.AddRange(cust, perf);
        await _db.SaveChangesAsync();

        var booking = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Unpaid Pilgrim"));
        // Status is PendingPayment

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bookingService.AcceptAsync(perf.Id, booking.Id));

        Assert.Contains("paid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TwoPerformers_AcceptingSameBooking_OnlyOneWins()
    {
        var cust = new User { Email = "race_cust@example.com", Name = "Cust", Role = Role.Customer };
        var perf1 = new User { Email = "perf1@example.com", Name = "Perf 1", Role = Role.Performer };
        var perf2 = new User { Email = "perf2@example.com", Name = "Perf 2", Role = Role.Performer };
        _db.Users.AddRange(cust, perf1, perf2);
        await _db.SaveChangesAsync();

        var booking = await _bookingService.CreateAsync(cust.Id, new CreateBookingRequest("Race Pilgrim"));
        booking.Status = BookingStatus.Paid;
        await _db.SaveChangesAsync();

        // Perf 1 accepts
        var acceptedBooking = await _bookingService.AcceptAsync(perf1.Id, booking.Id);
        Assert.Equal(perf1.Id, acceptedBooking.PerformerId);
        Assert.Equal(BookingStatus.Accepted, acceptedBooking.Status);

        // Perf 2 attempts to accept the already accepted booking
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bookingService.AcceptAsync(perf2.Id, booking.Id));

        Assert.Contains("already taken", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify perf 1 is still the only performer recorded in the db
        var fromDb = await _db.Bookings.FindAsync(booking.Id);
        Assert.Equal(perf1.Id, fromDb!.PerformerId);
    }
}

