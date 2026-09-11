using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account;

public class RegisterConfirmationModel : PageModel
{
    public string? Email { get; private set; }

    public void OnGet(string? email)
    {
        Email = email;
    }
}
