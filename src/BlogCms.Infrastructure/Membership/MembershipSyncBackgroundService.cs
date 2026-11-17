using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Membership;

/// <summary>
/// Periodically refreshes known Stripe subscriptions. Does nothing while the
/// membership feature is disabled.
/// </summary>
public sealed class MembershipSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MembershipOptions _options;
    private readonly ILogger<MembershipSyncBackgroundService> _logger;

    public MembershipSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<MembershipOptions> options,
        ILogger<MembershipSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_options.Enabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sync = scope.ServiceProvider.GetRequiredService<IMembershipSyncService>();
                    await sync.SyncAllAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Membership background sync failed.");
                }
            }

            var delay = TimeSpan.FromHours(Math.Max(1, _options.SyncIntervalHours));
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}