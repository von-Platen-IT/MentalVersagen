using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class LinkListItemConfiguration : IEntityTypeConfiguration<LinkListItem>
{
    public void Configure(EntityTypeBuilder<LinkListItem> builder)
    {
        builder.HasOne(i => i.LinkList)
            .WithMany(l => l.Items)
            .HasForeignKey(i => i.LinkListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Article)
            .WithMany(a => a.LinkListItems)
            .HasForeignKey(i => i.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.LinkListId, i.Position });
    }
}
