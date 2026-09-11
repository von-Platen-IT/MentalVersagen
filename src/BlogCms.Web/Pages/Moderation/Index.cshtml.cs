using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Comments;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Moderation;

[Authorize(Policy = Policies.RequireModerator)]
public class IndexModel : PageModel
{
    private readonly ICommentService _comments;

    public IndexModel(ICommentService comments)
    {
        _comments = comments;
    }

    public IReadOnlyList<ReportGroup> ReportGroups { get; private set; } = [];

    public IReadOnlyList<Comment> PendingAndFlagged { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ReportGroups = await _comments.GetOpenReportsAsync();
        PendingAndFlagged = await _comments.GetPendingAndFlaggedAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid commentId)
    {
        await _comments.ModerateAsync(commentId, approve: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid commentId)
    {
        await _comments.ModerateAsync(commentId, approve: false);
        await _comments.DismissReportsForCommentAsync(commentId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDismissReportsAsync(Guid commentId)
    {
        await _comments.DismissReportsForCommentAsync(commentId);
        return RedirectToPage();
    }
}
