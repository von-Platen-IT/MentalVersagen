using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlogCms.Infrastructure.Content;

/// <summary>
/// Periodically publishes articles whose planned publication time has been reached
/// (FeatureFix1 BR-032). Runs on a short interval; lightweight because it only
/// touches articles that are actually due.
/// </summary>
public sealed class ArticleSchedulerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArticleSchedulerService> _logger;

    public ArticleSchedulerService(IServiceScopeFactory scopeFactory, ILogger<ArticleSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First run shortly after startup so scheduled items are picked up promptly,
        // then continue on the regular interval.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var articles = scope.ServiceProvider.GetRequiredService<IArticleService>();
                var published = await articles.PublishDueScheduledAsync(stoppingToken);

                if (published > 0)
                {
                    _logger.LogInformation(
                        "Scheduled publication: {Count} article(s) published.", published);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Scheduled publication pass failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
