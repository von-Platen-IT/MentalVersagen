using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlogCms.Infrastructure.Newsletter;

/// <summary>
/// Background job that removes unconfirmed newsletter sign-ups after the
/// configured retention period, as required by the double opt-in flow.
/// </summary>
public sealed class NewsletterCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NewsletterCleanupService> _logger;

    public NewsletterCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<NewsletterCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var newsletterService = scope.ServiceProvider.GetRequiredService<INewsletterService>();
                var removed = await newsletterService.DeleteExpiredUnconfirmedAsync(stoppingToken);

                if (removed > 0)
                {
                    _logger.LogInformation("Removed {Count} unconfirmed newsletter sign-ups.", removed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Newsletter cleanup run failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
