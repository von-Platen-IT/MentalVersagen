using BlogCms.Infrastructure.Content;

namespace BlogCms.Web.Content;

/// <summary>German labels for the public article sort options.</summary>
public static class SortDisplay
{
    public static string Label(ArticleSortOrder order) => order switch
    {
        ArticleSortOrder.Newest => "Neu zu alt",
        ArticleSortOrder.Oldest => "Alt zu neu",
        ArticleSortOrder.Popular => "Beliebteste zuerst",
        ArticleSortOrder.MostCommented => "Meist kommentiert",
        ArticleSortOrder.MostUpvoted => "Meiste positive Bewertungen",
        ArticleSortOrder.MostDownvoted => "Meiste negative Bewertungen",
        ArticleSortOrder.MostLiked => "Meist geliked",
        ArticleSortOrder.Title => "Titel A–Z",
        _ => order.ToString()
    };
}
