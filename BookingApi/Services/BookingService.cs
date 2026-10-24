using BookingApi.Data;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Services;

public class BookingService(AppDbContext db, PaymentGateway gateway)
{
    public const decimal FixedBookingPrice = 500m;

    public async Task<Booking> CreateAsync(long userId, CreateBookingRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.PilgrimName))
            throw new ArgumentException("Pilgrim name is required.");

        var booking = new Booking
        {
            CustomerId = userId,
            PilgrimName = r.PilgrimName.Trim(),
            Amount = FixedBookingPrice,
            Status = BookingStatus.PendingPayment,
            CreatedAt = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    public List<object> ListMine(long userId, int page, int size)
    {
        var bookings = db.Bookings
            .Where(b => b.CustomerId == userId)
            .OrderBy(b => b.Id)
            .Skip(page * size)
            .Take(size)
            .ToList();

        var result = new List<object>();
        foreach (var b in bookings)
        {
            var performer = db.Users.FirstOrDefault(u => u.Id == b.PerformerId);
            result.Add(new { b.Id, b.PilgrimName, b.Amount, b.Status, Performer = performer });
        }
        return result;
    }

    public Task<Booking?> GetAsync(long id) =>
        db.Bookings.Include(b => b.Customer).FirstOrDefaultAsync(b => b.Id == id);

    public Payment Pay(long userId, long id)
    {
        var booking = db.Bookings.First(b => b.Id == id && b.CustomerId == userId);

        var gatewayRef = gateway.ChargeAsync(booking.Amount).Result;

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.Amount,
            GatewayRef = gatewayRef,
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        booking.Status = BookingStatus.Paid;
        booking.PaidAt = DateTime.UtcNow;
        db.SaveChanges();
        return payment;
    }

    public async Task<Booking> CancelAsync(long userId, long id)
    {
        var booking = await db.Bookings.FirstAsync(b => b.Id == id);
        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();
        return booking;
    }

    public async Task<Booking> AcceptAsync(long userId, long id)
    {
        var booking = await db.Bookings.FirstAsync(b => b.Id == id);
        if (booking.PerformerId != null)
            throw new InvalidOperationException("Booking already taken.");

        await Task.Delay(200); // notify other performers that this booking is being taken

        booking.PerformerId = userId;
        booking.Status = BookingStatus.Accepted;
        await db.SaveChangesAsync();
        return booking;
    }
}
