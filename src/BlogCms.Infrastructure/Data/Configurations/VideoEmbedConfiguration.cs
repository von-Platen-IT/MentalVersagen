using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class VideoEmbedConfiguration : IEntityTypeConfiguration<VideoEmbed>
{
    public void Configure(EntityTypeBuilder<VideoEmbed> builder)
    {
        builder.Property(v => v.OriginalUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(v => v.EmbedHtml)
            .IsRequired();

        builder.Property(v => v.ThumbnailUrl)
            .HasMaxLength(2000);

        builder.HasOne(v => v.Article)
            .WithMany(a => a.VideoEmbeds)
            .HasForeignKey(v => v.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Comment)
            .WithMany(c => c.VideoEmbeds)
            .HasForeignKey(v => v.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
