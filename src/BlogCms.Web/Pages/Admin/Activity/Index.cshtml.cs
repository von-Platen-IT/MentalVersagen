using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Activity;

/// <summary>
/// Admin-only evaluation of the unified activity log (KISS: one page, a few
/// aggregate tables and a date-range filter).
/// </summary>
[Authorize(Policy = Policies.RequireAdmin)]
public class IndexModel : PageModel
{
    private readonly IActivityLogService _activity;

    public IndexModel(IActivityLogService activity)
    {
        _activity = activity;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    public DateTime FromDate { get; private set; }

    public DateTime ToDate { get; private set; }

    public ActivityOverview Overview { get; private set; } = new(
        0, 0, 0, 0, 0,
        Array.Empty<ArticleActivity>(),
        Array.Empty<ArticleRatingActivity>(),
        Array.Empty<DailyActivity>(),
        Array.Empty<ActivityLogEntry>());

    public async Task OnGetAsync()
    {
        var today = DateTime.UtcNow.Date;
        FromDate = (From ?? today.AddDays(-29)).Date;
        ToDate = (To ?? today).Date;
        if (ToDate < FromDate)
        {
            ToDate = FromDate;
        }

        // Half-open interval [from, to + 1 day).
        Overview = await _activity.GetOverviewAsync(FromDate, ToDate.AddDays(1));
    }

    /// <summary>German label for an event type (used in the recent-entries table).</summary>
    public static string EventLabel(ActivityEventType type) => type switch
    {
        ActivityEventType.ArticleViewed => "Aufruf",
        ActivityEventType.ArticleCreated => "Beitrag erstellt",
        ActivityEventType.CommentCreated => "Kommentar",
        ActivityEventType.ArticleRated => "Bewertung",
        _ => type.ToString()
    };
}
