using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Authorization;
using BlogCms.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Articles;

[Authorize(Policy = Policies.RequireAdmin)]
public class EditModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly IOEmbedService _oEmbed;

    public EditModel(IArticleService articles, IMediaService media, IOEmbedService oEmbed)
    {
        _articles = articles;
        _media = media;
        _oEmbed = oEmbed;
    }

    [BindProperty]
    public ArticleInputModel Input { get; set; } = new();

    public Guid ArticleId { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var article = await _articles.GetByIdAsync(id);
        if (article is null)
        {
            return NotFound();
        }

        ArticleId = article.Id;
        Input = new ArticleInputModel
        {
            Title = article.Title,
            Slug = article.Slug,
            ContentMarkdown = article.ContentMarkdown,
            Excerpt = article.Excerpt,
            Category = article.Category,
            IsPremium = article.IsPremium,
            Status = article.Status,
            Tags = string.Join(", ", article.ArticleTags.Select(at => at.Tag!.Name)),
            VideoUrl = article.VideoEmbeds.FirstOrDefault()?.OriginalUrl
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        ArticleId = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var article = await _articles.GetByIdAsync(id);
        if (article is null)
        {
            return NotFound();
        }

        article.Title = Input.Title;
        article.Slug = Input.Slug ?? string.Empty;
        article.ContentMarkdown = Input.ContentMarkdown;
        article.Excerpt = Input.Excerpt;
        article.Category = Input.Category;
        article.IsPremium = Input.IsPremium;
        article.Status = Input.Status;

        await _articles.UpdateAsync(article, ArticleInputModel.ParseTags(Input.Tags));

        if (!string.IsNullOrWhiteSpace(Input.VideoUrl))
        {
            var resolved = await _oEmbed.ResolveAsync(Input.VideoUrl);
            await _articles.SetVideoEmbedAsync(article.Id, Input.VideoUrl, resolved);
        }

        if (Input.ImageUpload is { Length: > 0 })
        {
            await using var stream = Input.ImageUpload.OpenReadStream();
            var uploadedBy = article.AuthorId;
            await _media.UploadAsync(
                stream, Input.ImageUpload.FileName, MediaOwnerType.Article, article.Id, uploadedBy, null);
        }

        TempData["Message"] = $"Artikel „{article.Title}“ wurde aktualisiert.";
        return RedirectToPage("Index");
    }
}
