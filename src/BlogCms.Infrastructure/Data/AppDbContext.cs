using BlogCms.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlogCms.Infrastructure.Data;

/// <summary>
/// Application database context. Derives from <see cref="IdentityDbContext{TUser, TRole, TKey}"/>
/// so that ASP.NET Core Identity tables live in the same database as the domain model.
/// </summary>
public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<VideoEmbed> VideoEmbeds => Set<VideoEmbed>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all IEntityTypeConfiguration<T> from this assembly.
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Store every enum as a readable string (see DataSchema.md conventions).
        // Only applied for relational providers (PostgreSQL); the in-memory provider
        // used by tests keeps native enums so enum comparisons translate correctly.
        if (Database.IsRelational())
        {
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    var propertyType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    if (propertyType.IsEnum)
                    {
                        var converterType = typeof(EnumToStringConverter<>).MakeGenericType(propertyType);
                        var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
                        property.SetValueConverter(converter);
                    }
                }
            }
        }
    }
}
