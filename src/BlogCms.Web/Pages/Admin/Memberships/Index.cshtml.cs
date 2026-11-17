using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Membership;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Pages.Admin.Memberships;

/// <summary>
/// Admin overview of the new membership feature. Grants and revokes complimentary
/// access and writes an audit entry for every change. The page is only reachable
/// while <c>Membership:Enabled</c> is true and requires the Admin role.
/// </summary>
[Authorize(Policy = Policies.RequireAdmin)]
public class IndexModel : PageModel
{
    private readonly IMembershipService _membership;
    private readonly IMembershipSyncService _sync;
    private readonly UserManager<User> _userManager;
    private readonly MembershipOptions _options;

    public IndexModel(
        IMembershipService membership,
        IMembershipSyncService sync,
        UserManager<User> userManager,
        IOptions<MembershipOptions> options)
    {
        _membership = membership;
        _sync = sync;
        _userManager = userManager;
        _options = options.Value;
    }

    public IReadOnlyList<MembershipAdminRow> Rows { get; private set; } = [];

    public int TotalCount => Rows.Count;

    public int ActiveCount =>
        Rows.Count(r => r.Profile?.Status is MembershipStatus.Active or MembershipStatus.Trialing);

    public int PastDueCount =>
        Rows.Count(r => r.Profile?.Status == MembershipStatus.PastDue);

    public int CanceledCount =>
        Rows.Count(r => r.Profile?.Status == MembershipStatus.Canceled);

    public int ComplimentaryCount =>
        Rows.Count(r => r.Profile is { IsComplimentary: true });

    public int BasicCount =>
        Rows.Count(r => !IsActive(r.Profile) && !IsComplimentary(r.Profile));

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public MembershipFilter Filter { get; set; } = MembershipFilter.All;

    [BindProperty]
    public Guid TargetUserId { get; set; }

    [BindProperty]
    public DateTime? Until { get; set; }

    [BindProperty]
    public string? Note { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        Rows = await _membership.GetForAdminAsync(Search, Filter);
        return Page();
    }

    public async Task<IActionResult> OnPostGrantAsync()
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        var result = await _membership.GrantComplimentaryAsync(
            TargetUserId, CurrentUserId(), NormalizeUntil(Until), Note);

        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? "Die Freigabe wurde gesetzt."
            : result.Error;

        return RedirectToPage(new { Search, Filter });
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid targetUserId)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        var result = await _membership.RevokeComplimentaryAsync(
            targetUserId, CurrentUserId(), Note);

        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? "Die Freigabe wurde entzogen."
            : result.Error;

        return RedirectToPage(new { Search, Filter });
    }

    public async Task<IActionResult> OnPostSyncAsync(
        Guid targetUserId, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        var updated = await _sync.SyncUserAsync(targetUserId, cancellationToken);
        TempData[updated ? "Message" : "Error"] = updated
            ? "Der Stripe-Status wurde aktualisiert."
            : "Für diesen Benutzer ist kein Stripe-Abo hinterlegt oder der Abruf ist fehlgeschlagen.";

        return RedirectToPage(new { Search, Filter });
    }

    public string StatusLabel(MembershipProfile? profile)
    {
        if (profile is null)
        {
            return "Basis";
        }

        if (profile.IsComplimentary)
        {
            return "Freigeschaltet";
        }

        return profile.Status switch
        {
            MembershipStatus.Active or MembershipStatus.Trialing => "Mitglied",
            MembershipStatus.PastDue => "Zahlung offen",
            MembershipStatus.Canceled => "Gekündigt",
            MembershipStatus.Unpaid => "Unbezahlt",
            _ => "Basis"
        };
    }

    public string StatusPillClass(MembershipProfile? profile)
    {
        if (profile is null)
        {
            return "mv-pill";
        }

        if (profile.IsComplimentary)
        {
            return "mv-pill";
        }

        return profile.Status switch
        {
            MembershipStatus.Active or MembershipStatus.Trialing => "mv-pill mv-text-success",
            MembershipStatus.PastDue or MembershipStatus.Unpaid => "mv-pill mv-text-danger",
            _ => "mv-pill"
        };
    }

    private static bool IsActive(MembershipProfile? profile) =>
        profile?.Status is MembershipStatus.Active or MembershipStatus.Trialing;

    private static bool IsComplimentary(MembershipProfile? profile) =>
        profile is { IsComplimentary: true };

    private Guid CurrentUserId() =>
        Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : Guid.Empty;

    /// <summary>
    /// Treats the date picker value as "valid through the end of that local day"
    /// and converts it to UTC.
    /// </summary>
    private static DateTime? NormalizeUntil(DateTime? until)
    {
        if (until is null)
        {
            return null;
        }

        var localEndOfDay = DateTime.SpecifyKind(
            until.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
        return localEndOfDay.ToUniversalTime();
    }
}