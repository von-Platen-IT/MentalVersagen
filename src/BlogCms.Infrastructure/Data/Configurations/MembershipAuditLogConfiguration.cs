using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class MembershipAuditLogConfiguration : IEntityTypeConfiguration<MembershipAuditLog>
{
    public void Configure(EntityTypeBuilder<MembershipAuditLog> builder)
    {
        builder.Property(l => l.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.Details)
            .HasMaxLength(1000);

        builder.HasIndex(l => l.TargetUserId);
        builder.HasIndex(l => l.CreatedAt);
    }
}