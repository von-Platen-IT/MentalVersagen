using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(a => a.Slug)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(a => a.ContentMarkdown)
            .IsRequired();

        builder.Property(a => a.Excerpt)
            .HasMaxLength(500);

        // Slug must be unique (collisions prevented at save time).
        builder.HasIndex(a => a.Slug)
            .IsUnique();

        builder.HasOne(a => a.Author)
            .WithMany(u => u.Articles)
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete: deleted articles are hidden from normal queries.
        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
