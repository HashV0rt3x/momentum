using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Momentum.Domain.Auth;
using Momentum.Infrastructure.Identity;

namespace Momentum.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        // SHA-256 hex is exactly 64 chars. Unique: lookups happen by hash alone.
        builder.Property(t => t.TokenHash).HasMaxLength(64);
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasIndex(t => t.UserId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
