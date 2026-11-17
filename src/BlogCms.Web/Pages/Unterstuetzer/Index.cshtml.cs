using BlogCms.Infrastructure.Membership;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Pages.Unterstuetzer;

/// <summary>
/// Landing page for memberships and donations. The donation flow is still a
/// placeholder; the membership checkout uses the new /membership/checkout
/// endpoint when the feature is enabled.
/// </summary>
public class IndexModel : PageModel
{
    private readonly MembershipOptions _membershipOptions;

    public IndexModel(IOptions<MembershipOptions> membershipOptions)
    {
        _membershipOptions = membershipOptions.Value;
    }

    public bool MembershipEnabled => _membershipOptions.Enabled;

    public void OnGet()
    {
    }
}
