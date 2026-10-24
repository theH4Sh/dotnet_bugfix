using System.Text.Json.Serialization;

namespace BookingApi.Models;

public enum Role { Customer = 1, Performer = 2 }

public enum BookingStatus { PendingPayment = 1, Paid = 2, Accepted = 3, Completed = 4, Cancelled = 5, Expired = 6, PaymentProcessing = 7 }

public class User
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    [JsonIgnore]
    public string Password { get; set; } = "";
    public string Name { get; set; } = "";
    public Role Role { get; set; }
}

public class Booking
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public User Customer { get; set; } = null!;
    public long? PerformerId { get; set; }
    public User? Performer { get; set; }
    public string PilgrimName { get; set; } = "";
    public decimal Amount { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class Payment
{
    public long Id { get; set; }
    public long BookingId { get; set; }
    public decimal Amount { get; set; }
    public string GatewayRef { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public record RegisterRequest(string Email, string Password, string Name, Role Role);
public record UserResponse(long Id, string Email, string Name, Role Role);
public record LoginRequest(string Email, string Password);
public record FirebaseLoginRequest(string IdToken);
public record CreateBookingRequest(string PilgrimName, decimal? Amount = null);
