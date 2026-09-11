using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.Property(s => s.StripeCustomerId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.StripeSubscriptionId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.PlanId)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(s => s.StripeSubscriptionId).IsUnique();

        builder.HasOne(s => s.User)
            .WithMany(u => u.Subscriptions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
