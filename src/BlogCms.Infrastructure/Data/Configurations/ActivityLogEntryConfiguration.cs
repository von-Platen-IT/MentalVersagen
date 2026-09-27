using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
{
    public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
    {
        // 45 chars covers the longest IPv6 representation (with IPv4-mapped form).
        builder.Property(e => e.IpAddress)
            .HasMaxLength(45);

        // Keep the log entry when a user account is removed (anonymized soft-delete),
        // so historical aggregates stay intact.
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Article)
            .WithMany()
            .HasForeignKey(e => e.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Comment)
            .WithMany()
            .HasForeignKey(e => e.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Supports the admin evaluation: per-article counts and time-range queries.
        builder.HasIndex(e => new { e.ArticleId, e.EventType });
        builder.HasIndex(e => e.CreatedAt);
    }
}
