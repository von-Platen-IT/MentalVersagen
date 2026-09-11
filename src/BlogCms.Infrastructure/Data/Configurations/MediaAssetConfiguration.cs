using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.Property(m => m.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(m => m.MimeType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.AltText)
            .HasMaxLength(500);

        builder.HasOne(m => m.Article)
            .WithMany(a => a.MediaAssets)
            .HasForeignKey(m => m.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Comment)
            .WithMany(c => c.MediaAssets)
            .HasForeignKey(m => m.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.UploadedByUser)
            .WithMany(u => u.MediaAssets)
            .HasForeignKey(m => m.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
