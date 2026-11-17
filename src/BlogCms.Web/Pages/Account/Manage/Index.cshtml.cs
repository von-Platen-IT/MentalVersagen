using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Membership;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IMembershipService _membership;
    private readonly MembershipOptions _membershipOptions;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IMembershipService membership,
        IOptions<MembershipOptions> membershipOptions,
        ILogger<IndexModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _membership = membership;
        _membershipOptions = membershipOptions.Value;
        _logger = logger;
    }

    public User? CurrentUser { get; private set; }

    public MembershipProfile? MembershipProfile { get; private set; }

    public bool MembershipEnabled => _membershipOptions.Enabled;

    public bool CanOpenPortal =>
        MembershipProfile is not null &&
        !string.IsNullOrWhiteSpace(MembershipProfile.StripeCustomerId);

    public DateTime? MembershipPeriodEnd => MembershipProfile?.CurrentPeriodEnd;

    public string MembershipStatusLabel => MembershipProfile switch
    {
        null => "Keine Mitgliedschaft",
        { IsComplimentary: true } => "Freigeschaltet",
        { Status: MembershipStatus.Active or MembershipStatus.Trialing } => "Aktiv",
        { Status: MembershipStatus.PastDue } => "Zahlung offen",
        { Status: MembershipStatus.Canceled } => "Gekündigt",
        { Status: MembershipStatus.Unpaid } => "Unbezahlt",
        _ => "Inaktiv"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentUser = await _userManager.GetUserAsync(User);
        if (CurrentUser is null)
        {
            return RedirectToPage("/Account/Login");
        }

        MembershipProfile = await _membership.GetProfileAsync(CurrentUser.Id);

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAccountAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Soft-delete + anonymize personal data, while keeping authored content
        // structurally intact (articles/comments then show "Gelöschter Nutzer").
        var anonymizedEmail = $"deleted-{user.Id:N}@deleted.invalid";
        user.Email = anonymizedEmail;
        user.UserName = anonymizedEmail;
        user.NormalizedEmail = anonymizedEmail.ToUpperInvariant();
        user.NormalizedUserName = anonymizedEmail.ToUpperInvariant();
        user.DisplayName = "Gelöschter Nutzer";
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.DeletedAt = DateTime.UtcNow;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("User {UserId} deleted their account (soft-delete + anonymization).", user.Id);

        await _signInManager.SignOutAsync();

        return RedirectToPage("/Account/AccountDeleted");
    }
}
