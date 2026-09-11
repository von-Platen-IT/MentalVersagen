using BlogCms.Infrastructure.Newsletter;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Newsletter;

public class UnsubscribeModel : PageModel
{
    private readonly INewsletterService _newsletterService;

    public UnsubscribeModel(INewsletterService newsletterService)
    {
        _newsletterService = newsletterService;
    }

    public bool Succeeded { get; private set; }

    public async Task OnGetAsync(string? token, CancellationToken cancellationToken)
    {
        Succeeded = !string.IsNullOrWhiteSpace(token) &&
                    await _newsletterService.UnsubscribeAsync(token, cancellationToken);
    }
}
