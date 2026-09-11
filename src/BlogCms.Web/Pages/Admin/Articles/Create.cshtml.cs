using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Authorization;
using BlogCms.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Articles;

[Authorize(Policy = Policies.RequireAdmin)]
public class CreateModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly IOEmbedService _oEmbed;
    private readonly UserManager<User> _userManager;

    public CreateModel(
        IArticleService articles,
        IMediaService media,
        IOEmbedService oEmbed,
        UserManager<User> userManager)
    {
        _articles = articles;
        _media = media;
        _oEmbed = oEmbed;
        _userManager = userManager;
    }

    [BindProperty]
    public ArticleInputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var authorIdValue = _userManager.GetUserId(User);
        if (authorIdValue is null || !Guid.TryParse(authorIdValue, out var authorId))
        {
            return Challenge();
        }

        var article = new Article
        {
            Title = Input.Title,
            Slug = Input.Slug ?? string.Empty,
            ContentMarkdown = Input.ContentMarkdown,
            Excerpt = Input.Excerpt,
            Category = Input.Category,
            IsPremium = Input.IsPremium,
            Status = Input.Status,
            AuthorId = authorId
        };

        var created = await _articles.CreateAsync(article, ArticleInputModel.ParseTags(Input.Tags));

        await ApplyMediaAsync(created.Id, authorId);

        TempData["Message"] = $"Artikel „{article.Title}“ wurde angelegt.";
        return RedirectToPage("Index");
    }

    private async Task ApplyMediaAsync(Guid articleId, Guid authorId)
    {
        if (!string.IsNullOrWhiteSpace(Input.VideoUrl))
        {
            var resolved = await _oEmbed.ResolveAsync(Input.VideoUrl);
            await _articles.SetVideoEmbedAsync(articleId, Input.VideoUrl, resolved);
        }

        if (Input.ImageUpload is { Length: > 0 })
        {
            await using var stream = Input.ImageUpload.OpenReadStream();
            await _media.UploadAsync(
                stream, Input.ImageUpload.FileName, MediaOwnerType.Article, articleId, authorId, null);
        }
    }
}
