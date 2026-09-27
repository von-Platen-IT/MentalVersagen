using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Donate;

/// <summary>
/// The former "Spenden" page was merged into <c>/Unterstuetzer</c>.
/// Kept as a permanent redirect so old links keep working.
/// </summary>
public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPagePermanent("/Unterstuetzer/Index");
}
