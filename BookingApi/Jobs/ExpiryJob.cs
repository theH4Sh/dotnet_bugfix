
using BookingApi.Data;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Jobs;

public class ExpiryJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddMinutes(-30);

                await db.Bookings
                    .Where(b =>
                        b.Status == BookingStatus.PendingPayment &&
                        b.CreatedAt < cutoff)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(b => b.Status, BookingStatus.Expired),
                        stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Booking expiry job failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
