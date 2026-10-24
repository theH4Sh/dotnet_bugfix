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

    public async Task<List<object>> ListMineAsync(long userId, int page = 1, int size = 10)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        var user = await db.Users.FindAsync(userId);
        if (user is null) return [];

        IQueryable<Booking> query = user.Role == Role.Performer
            ? db.Bookings.Where(b => b.PerformerId == userId)
            : db.Bookings.Where(b => b.CustomerId == userId);

        var bookings = await query
            .OrderByDescending(b => b.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var result = new List<object>();
        foreach (var b in bookings)
        {
            var performer = b.PerformerId.HasValue
                ? await db.Users.FirstOrDefaultAsync(u => u.Id == b.PerformerId.Value)
                : null;
            result.Add(new { b.Id, b.PilgrimName, b.Amount, b.Status, Performer = performer });
        }
        return result;
    }

    public async Task<List<object>> ListAvailablePaidAsync(int page = 1, int size = 10)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        var bookings = await db.Bookings
            .Where(b => b.Status == BookingStatus.Paid && b.PerformerId == null)
            .OrderByDescending(b => b.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return bookings.Select(b => (object)new
        {
            b.Id,
            b.PilgrimName,
            b.Amount,
            b.Status,
            b.CreatedAt,
            b.PaidAt
        }).ToList();
    }

    public async Task<Booking?> GetAsync(long userId, long id)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null)
            return null;

        var booking = await db.Bookings.Include(b => b.Customer).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null)
            return null;

        if (user.Role == Role.Customer)
        {
            if (booking.CustomerId != userId)
                throw new UnauthorizedAccessException("You are not authorized to view this booking.");
        }
        else if (user.Role == Role.Performer)
        {
            // Performers only get to see paid bookings or bookings they have accepted
            if (booking.Status != BookingStatus.Paid && booking.PerformerId != userId)
                throw new UnauthorizedAccessException("Performers can only view paid bookings or bookings assigned to them.");
        }

        return booking;
    }

    public async Task<Payment> PayAsync(long userId, long id)
    {
        var claimed = await db.Bookings
            .Where(b => b.Id == id && b.CustomerId == userId && b.Status == BookingStatus.PendingPayment)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.PaymentProcessing));

        if (claimed == 0)
        {
            var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
            if (booking is null)
                throw new KeyNotFoundException("Booking not found.");
            if (booking.CustomerId != userId)
                throw new UnauthorizedAccessException("You are not authorized to pay for this booking.");

            throw new InvalidOperationException("Booking has already been paid or a payment is in progress.");
        }

        var claimedBooking = await db.Bookings.SingleAsync(b => b.Id == id);
        var gatewayRef = await gateway.ChargeAsync(claimedBooking.Amount);

        var payment = new Payment
        {
            BookingId = claimedBooking.Id,
            Amount = claimedBooking.Amount,
            GatewayRef = gatewayRef,
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        claimedBooking.Status = BookingStatus.Paid;
        claimedBooking.PaidAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return payment;
    }

    public async Task<Booking> CancelAsync(long userId, long id)
    {
        var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null)
            throw new KeyNotFoundException("Booking not found.");

        if (booking.CustomerId != userId)
            throw new UnauthorizedAccessException("You are not authorized to cancel this booking.");

        if (booking.Status == BookingStatus.Accepted || booking.PerformerId != null)
            throw new InvalidOperationException("Cannot cancel a booking that has already been accepted.");

        if (booking.Status != BookingStatus.PendingPayment && booking.Status != BookingStatus.Paid)
            throw new InvalidOperationException($"Cannot cancel a booking with status '{booking.Status}'.");

        var cancelled = await db.Bookings
            .Where(b =>
                b.Id == id &&
                b.CustomerId == userId &&
                b.PerformerId == null &&
                (b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Paid))
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled));

        if (cancelled == 0)
            throw new InvalidOperationException("Booking status changed before it could be cancelled.");

        booking.Status = BookingStatus.Cancelled;
        return booking;
    }

    public async Task<Booking> AcceptAsync(long userId, long id)
    {
        var performer = await db.Users.FindAsync(userId);
        if (performer is null || performer.Role != Role.Performer)
            throw new UnauthorizedAccessException("Only performers can accept bookings.");

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null)
            throw new KeyNotFoundException("Booking not found.");

        if (booking.CustomerId == userId)
            throw new InvalidOperationException("Performers cannot accept their own booking.");

        if (booking.PerformerId != null || booking.Status == BookingStatus.Accepted)
            throw new InvalidOperationException("Booking already taken.");

        if (booking.Status != BookingStatus.Paid)
            throw new InvalidOperationException("Only paid bookings can be accepted.");

        var affected = await db.Bookings
            .Where(b => b.Id == id && b.Status == BookingStatus.Paid && b.PerformerId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.PerformerId, userId)
                .SetProperty(b => b.Status, BookingStatus.Accepted));

        if (affected == 0)
            throw new InvalidOperationException("Booking already taken.");

        await db.Entry(booking).ReloadAsync();
        return booking;
    }
}
