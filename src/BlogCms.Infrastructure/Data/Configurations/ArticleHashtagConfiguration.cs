using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class ArticleHashtagConfiguration : IEntityTypeConfiguration<ArticleHashtag>
{
    public void Configure(EntityTypeBuilder<ArticleHashtag> builder)
    {
        builder.HasKey(ah => new { ah.ArticleId, ah.HashtagId });

        builder.HasOne(ah => ah.Article)
            .WithMany(a => a.ArticleHashtags)
            .HasForeignKey(ah => ah.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ah => ah.Hashtag)
            .WithMany(h => h.ArticleHashtags)
            .HasForeignKey(ah => ah.HashtagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
