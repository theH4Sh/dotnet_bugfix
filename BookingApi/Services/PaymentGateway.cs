namespace BookingApi.Services;

/// <summary>Fake payment gateway. Pretend this calls HyperPay and charges a real card.</summary>
public class PaymentGateway
{
    public async Task<string> ChargeAsync(decimal amount)
    {
        await Task.Delay(300);
        return "ch_" + Guid.NewGuid().ToString("N")[..12];
    }
}
