using BlogCms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogCms.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        // Email uniqueness is provided by ASP.NET Core Identity's base configuration.
        // Note: deleted users are NOT globally filtered because their authored
        // content must remain visible (shown as "Gelöschter Nutzer" in the UI).
    }
}
