using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.Property(d => d.Amount)
            .HasPrecision(18, 2);

        builder.Property(d => d.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(d => d.ProviderTransactionId)
            .IsRequired()
            .HasMaxLength(255);

        // Nullable FK: anonymous donations have no linked user.
        builder.HasOne(d => d.User)
            .WithMany(u => u.Donations)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
