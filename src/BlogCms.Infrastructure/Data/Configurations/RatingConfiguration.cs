using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.HasOne(r => r.User)
            .WithMany(u => u.Ratings)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Article)
            .WithMany(a => a.Ratings)
            .HasForeignKey(r => r.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Comment)
            .WithMany(c => c.Ratings)
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // At most one rating per user and target. NULLs are distinct in PostgreSQL, so
        // the "other" column being null does not cause collisions.
        builder.HasIndex(r => new { r.UserId, r.ArticleId }).IsUnique();
        builder.HasIndex(r => new { r.UserId, r.CommentId }).IsUnique();
    }
}
