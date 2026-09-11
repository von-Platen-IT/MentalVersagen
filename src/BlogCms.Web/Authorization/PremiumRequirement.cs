using Microsoft.AspNetCore.Authorization;

namespace BlogCms.Web.Authorization;

/// <summary>
/// Requirement satisfied when the current user has an active subscription
/// (or is an admin). Evaluated by <see cref="PremiumAuthorizationHandler"/>.
/// </summary>
public sealed class PremiumRequirement : IAuthorizationRequirement
{
}
