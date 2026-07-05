using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Momentum.Infrastructure.Identity;

namespace Momentum.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasIndex(u => u.TelegramId).IsUnique();

        builder.Property(u => u.Name).HasMaxLength(200);
        builder.Property(u => u.TelegramUsername).HasMaxLength(64);
        builder.Property(u => u.AvatarUrl).HasMaxLength(512);
        builder.Property(u => u.JobTitle).HasMaxLength(200);
        builder.Property(u => u.Locale).HasMaxLength(8);
        builder.Property(u => u.TimeZoneId).HasMaxLength(64);

        // Stored as text ("Owner", "Member", ...) so the DB stays readable and
        // reordering the enum can never silently remap roles.
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
    }
}
