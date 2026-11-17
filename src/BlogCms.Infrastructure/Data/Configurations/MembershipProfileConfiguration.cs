using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class MembershipProfileConfiguration : IEntityTypeConfiguration<MembershipProfile>
{
    public void Configure(EntityTypeBuilder<MembershipProfile> builder)
    {
        builder.Property(p => p.StripeCustomerId).HasMaxLength(200);
        builder.Property(p => p.StripeSubscriptionId).HasMaxLength(200);
        builder.Property(p => p.PlanId).HasMaxLength(200);

        builder.HasIndex(p => p.UserId).IsUnique();
        builder.HasIndex(p => p.StripeCustomerId);
        builder.HasIndex(p => p.StripeSubscriptionId);

        builder.HasOne(p => p.User)
            .WithOne(u => u.MembershipProfile)
            .HasForeignKey<MembershipProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}