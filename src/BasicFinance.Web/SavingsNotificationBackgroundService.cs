using BasicFinance.Web.Services;

namespace BasicFinance.Web.Services;

public sealed class SavingsNotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SavingsNotificationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<SavingsNotificationService>();
                var created = await notificationService.GenerateAsync(DateTime.Today, stoppingToken);
                if (created > 0)
                    logger.LogInformation("Generated {Count} savings notifications.", created);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Savings notification generation failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
