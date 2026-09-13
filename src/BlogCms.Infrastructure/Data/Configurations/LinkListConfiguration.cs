using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class LinkListConfiguration : IEntityTypeConfiguration<LinkList>
{
    public void Configure(EntityTypeBuilder<LinkList> builder)
    {
        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Slug)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Description)
            .HasMaxLength(500);

        builder.HasIndex(l => l.Slug).IsUnique();
    }
}
