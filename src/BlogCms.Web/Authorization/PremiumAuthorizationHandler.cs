using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BlogCms.Web.Authorization;

/// <summary>
/// Authorizes premium access based on the user's current subscription status.
/// Per the requirements, this must not rely on the <c>Premium</c> role alone,
/// to avoid delays between payment state and role assignment.
/// </summary>
public sealed class PremiumAuthorizationHandler : AuthorizationHandler<PremiumRequirement>
{
    private readonly UserManager<User> _userManager;

    public PremiumAuthorizationHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PremiumRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Admins always have access.
        if (context.User.IsInRole(nameof(UserRole.Admin)))
        {
            context.Succeed(requirement);
            return;
        }

        var user = await _userManager.GetUserAsync(context.User);
        if (user is null || user.DeletedAt is not null)
        {
            return;
        }

        if (user.SubscriptionStatus == UserSubscriptionStatus.Active)
        {
            context.Succeed(requirement);
        }
    }
}
