using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlogCms.Infrastructure.Content;

/// <summary>
/// Application service for the article posting workflow (create and edit). It owns
/// the business rules (validation, target status), orchestrates persistence, media,
/// video embedding and the activity log inside a single transaction and reports
/// field-level errors via <see cref="PostResult"/> instead of throwing.
/// </summary>
public interface IArticlePostingService
{
    /// <summary>Fachliche Regeln (BR-022 Titelbild-Quelle, BR-032 geplante Veröffentlichung).</summary>
    IReadOnlyList<PostError> Validate(ArticlePostRequest request, bool hasExistingImage);

    Task<PostResult> CreateAsync(
        ArticlePostRequest request, Guid authorId,
        string? clientIp = null, CancellationToken cancellationToken = default);

    Task<PostResult> UpdateAsync(
        Guid articleId, ArticlePostRequest request, Guid actorId,
        CancellationToken cancellationToken = default);
}

public sealed class ArticlePostingService : IArticlePostingService
{
    private readonly AppDbContext _db;
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly IOEmbedService _oEmbed;
    private readonly IActivityLogService _activity;

    public ArticlePostingService(
        AppDbContext db,
        IArticleService articles,
        IMediaService media,
        IOEmbedService oEmbed,
        IActivityLogService activity)
    {
        _db = db;
        _articles = articles;
        _media = media;
        _oEmbed = oEmbed;
        _activity = activity;
    }

    public IReadOnlyList<PostError> Validate(ArticlePostRequest request, bool hasExistingImage) =>
        ArticleValidator.Validate(request, hasExistingImage);

    public async Task<PostResult> CreateAsync(
        ArticlePostRequest request, Guid authorId,
        string? clientIp = null, CancellationToken cancellationToken = default)
    {
        await using var _ = new UploadScope(request);

        var errors = ArticleValidator.Validate(request, hasExistingImage: false);
        if (errors.Count > 0)
        {
            return PostResult.Fail(errors.ToArray());
        }

        var status = ArticleValidator.ResolveTargetStatus(request.Status, request.ScheduledAt);
        var scheduledUtc = ArticleValidator.NormalizeScheduledAtToUtc(request.ScheduledAt);
        var article = new Article
        {
            Title = request.Title.Trim(),
            Slug = request.Slug ?? string.Empty,
            TitleImageUrl = string.IsNullOrWhiteSpace(request.TitleImageUrl) ? null : request.TitleImageUrl.Trim(),
            ContentMarkdown = request.ContentMarkdown,
            Excerpt = string.IsNullOrWhiteSpace(request.Excerpt) ? null : request.Excerpt.Trim(),
            Category = request.Category,
            AccessLevel = request.AccessLevel,
            Status = status,
            ScheduledAt = status == ArticleStatus.Scheduled ? scheduledUtc : null,
            AuthorId = authorId
        };

        await using var tx = await BeginTransactionAsync(cancellationToken);
        try
        {
            await _articles.PrepareCreateAsync(article, request.Tags, request.Hashtags, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            var mediaErrors = await ApplyMediaAsync(
                article, request, uploaderId: authorId, clearVideoWhenEmpty: false, cancellationToken);
            if (mediaErrors.Count > 0)
            {
                await RollbackAsync(tx, cancellationToken);
                return PostResult.Fail(mediaErrors.ToArray());
            }

            // Attach images uploaded through the Markdown editor before the article existed.
            await _media.AttachToArticleAsync(request.UploadedImageIds, article.Id, authorId, cancellationToken);

            // The log entry is written only after the article is complete (media included).
            await _activity.LogArticleCreatedAsync(article.Id, authorId, clientIp, cancellationToken);

            await CommitAsync(tx, cancellationToken);
            return PostResult.Ok(article);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollbackAsync(tx, cancellationToken);
            return PostResult.Fail(new PostError(
                PostError.GenericField, "Beim Speichern ist ein Fehler aufgetreten. Bitte erneut versuchen."));
        }
    }

    public async Task<PostResult> UpdateAsync(
        Guid articleId, ArticlePostRequest request, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        await using var _ = new UploadScope(request);

        var article = await _articles.GetByIdAsync(articleId, cancellationToken);
        if (article is null)
        {
            return PostResult.Fail(new PostError(PostError.GenericField, "Der Artikel wurde nicht gefunden."));
        }

        var errors = ArticleValidator.Validate(request, article.MediaAssets.Count > 0);
        if (errors.Count > 0)
        {
            return PostResult.Fail(errors.ToArray());
        }

        var status = ArticleValidator.ResolveTargetStatus(request.Status, request.ScheduledAt);
        var scheduledUtc = ArticleValidator.NormalizeScheduledAtToUtc(request.ScheduledAt);
        var originalSlug = article.Slug;

        article.Title = request.Title.Trim();
        article.Slug = request.Slug ?? string.Empty;
        article.TitleImageUrl = string.IsNullOrWhiteSpace(request.TitleImageUrl) ? null : request.TitleImageUrl.Trim();
        article.ContentMarkdown = request.ContentMarkdown;
        article.Excerpt = string.IsNullOrWhiteSpace(request.Excerpt) ? null : request.Excerpt.Trim();
        article.Category = request.Category;
        article.AccessLevel = request.AccessLevel;
        article.Status = status;
        article.ScheduledAt = status == ArticleStatus.Scheduled ? scheduledUtc : null;

        await using var tx = await BeginTransactionAsync(cancellationToken);
        try
        {
            await _articles.PrepareUpdateAsync(
                article, originalSlug, request.Tags, request.Hashtags, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            var mediaErrors = await ApplyMediaAsync(
                article, request, uploaderId: article.AuthorId, clearVideoWhenEmpty: true, cancellationToken);
            if (mediaErrors.Count > 0)
            {
                await RollbackAsync(tx, cancellationToken);
                return PostResult.Fail(mediaErrors.ToArray());
            }

            // Attach editor images uploaded by the current user (an admin may edit
            // another author's article).
            await _media.AttachToArticleAsync(request.UploadedImageIds, article.Id, actorId, cancellationToken);

            await CommitAsync(tx, cancellationToken);
            return PostResult.Ok(article);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollbackAsync(tx, cancellationToken);
            return PostResult.Fail(new PostError(
                PostError.GenericField, "Beim Speichern ist ein Fehler aufgetreten. Bitte erneut versuchen."));
        }
    }

    /// <summary>
    /// Video embedding and image uploads. Upload results are evaluated — a rejected
    /// file becomes a field error and aborts the whole operation (rollback).
    /// </summary>
    private async Task<List<PostError>> ApplyMediaAsync(
        Article article, ArticlePostRequest request, Guid uploaderId, bool clearVideoWhenEmpty,
        CancellationToken cancellationToken)
    {
        var errors = new List<PostError>();

        if (!string.IsNullOrWhiteSpace(request.VideoUrl))
        {
            // An oEmbed failure must not lose the article; the URL is stored as a link.
            OEmbedResult resolved;
            try
            {
                resolved = await _oEmbed.ResolveAsync(request.VideoUrl, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resolved = new OEmbedResult(VideoPlatform.Other, false, null, null);
            }

            await _articles.SetVideoEmbedAsync(article.Id, request.VideoUrl, resolved, cancellationToken);
        }
        else if (clearVideoWhenEmpty)
        {
            // Clearing the VideoUrl field on Edit removes existing video embeds.
            await _articles.SetVideoEmbedAsync(
                article.Id, string.Empty,
                new OEmbedResult(VideoPlatform.Other, false, null, null), cancellationToken);
        }

        if (request.CoverUpload is not null)
        {
            var result = await _media.UploadAsync(
                request.CoverUpload.Content, request.CoverUpload.FileName,
                MediaOwnerType.Article, article.Id, uploaderId, null, cancellationToken);
            if (!result.Succeeded || result.Asset is null)
            {
                errors.Add(new PostError(
                    PostError.ImageUploadField, result.Error ?? "Das Titelbild konnte nicht hochgeladen werden."));
            }
            else
            {
                // Explicitly uploaded cover image always becomes the active cover asset.
                await _media.SetCoverAsync(article.Id, result.Asset.Id, cancellationToken);
            }
        }

        foreach (var upload in request.ContentUploads)
        {
            var result = await _media.UploadAsync(
                upload.Content, upload.FileName,
                MediaOwnerType.Article, article.Id, uploaderId, null, cancellationToken);
            if (!result.Succeeded)
            {
                errors.Add(new PostError(
                    PostError.ContentUploadsField, result.Error ?? "Ein Bild konnte nicht hochgeladen werden."));
            }
        }

        return errors;
    }

    private readonly struct UploadScope(ArticlePostRequest request) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (request.CoverUpload is not null)
            {
                await request.CoverUpload.DisposeAsync();
            }

            foreach (var upload in request.ContentUploads)
            {
                await upload.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Transactions require a relational provider; the InMemory provider (used in
    /// tests) does not support them, so the guard keeps the service testable.
    /// </summary>
    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    private static async Task CommitAsync(IDbContextTransaction? tx, CancellationToken cancellationToken)
    {
        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }
    }

    private static async Task RollbackAsync(IDbContextTransaction? tx, CancellationToken cancellationToken)
    {
        if (tx is not null)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }
}