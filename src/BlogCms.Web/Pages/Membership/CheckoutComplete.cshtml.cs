using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Payments;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Membership;

public class CheckoutCompleteModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CheckoutCompleteModel> _logger;

    public CheckoutCompleteModel(
        ISubscriptionService subscriptionService,
        IWebHostEnvironment environment,
        ILogger<CheckoutCompleteModel> logger)
    {
        _subscriptionService = subscriptionService;
        _environment = environment;
        _logger = logger;
    }

    public bool ActivationSimulated { get; private set; }

    public async Task OnGetAsync(Guid? userId, string? planId, int simulated = 0)
    {
        // In development with the fake provider, the checkout redirects straight
        // back here. Activate the subscription to mirror the webhook outcome.
        if (_environment.IsDevelopment() && simulated == 1 && userId is not null)
        {
            await _subscriptionService.ActivateAsync(
                userId.Value,
                planId ?? string.Empty,
                stripeCustomerId: $"cus_fake_{userId}",
                stripeSubscriptionId: $"sub_fake_{userId}",
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1),
                status: SubscriptionStatus.Active);

            ActivationSimulated = true;
            _logger.LogInformation("Simulated subscription activation for user {UserId}.", userId);
        }
    }
}
