using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Membership;

/// <summary>
/// Informational page after a Stripe Checkout redirect. It intentionally does not
/// change any membership state; the signed webhook is the only source of truth.
/// </summary>
public class CheckoutCompleteModel : PageModel
{
    public void OnGet()
    {
    }
}
