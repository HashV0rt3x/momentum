using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Momentum.Domain.Auth;
using Momentum.Domain.Categories;
using Momentum.Domain.Focus;
using Momentum.Domain.Projects;
using Momentum.Domain.Settings;
using Momentum.Domain.Tasks;
using Momentum.Infrastructure.Identity;

namespace Momentum.Infrastructure.Persistence.Configurations;

public sealed class TelegramLoginCodeConfiguration : IEntityTypeConfiguration<TelegramLoginCode>
{
    public void Configure(EntityTypeBuilder<TelegramLoginCode> builder)
    {
        builder.ToTable("TelegramLoginCodes");
        builder.Property(c => c.Code).HasMaxLength(6);
        builder.HasIndex(c => c.Code);
        builder.HasIndex(c => c.ExpiresAtUtc);
        builder.Property(c => c.FirstName).HasMaxLength(200);
        builder.Property(c => c.LastName).HasMaxLength(200);
        builder.Property(c => c.Username).HasMaxLength(64);
        builder.Property(c => c.LanguageCode).HasMaxLength(8);
    }
}

public sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasIndex(s => s.UserId).IsUnique();
        builder.Property(s => s.Theme).HasMaxLength(16);
        builder.Property(s => s.WeekStart).HasMaxLength(8);
        builder.Property(s => s.NotificationChannel).HasMaxLength(16);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.Property(c => c.Key).HasMaxLength(64);
        builder.Property(c => c.Color).HasMaxLength(16);
        builder.Property(c => c.Icon).HasMaxLength(64);
        builder.HasIndex(c => new { c.UserId, c.Key }).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Color).HasMaxLength(16);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.HasIndex(p => p.UserId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.Property(t => t.Title).HasMaxLength(300);
        builder.Property(t => t.Description).HasMaxLength(10_000);
        builder.Property(t => t.CategoryKey).HasMaxLength(64);

        // Stored as text so the DB stays readable and enum reordering can never
        // silently remap values.
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(t => new { t.UserId, t.Status });
        builder.HasIndex(t => new { t.UserId, t.DueDate });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Subtasks)
            .WithOne()
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FocusSessionConfiguration : IEntityTypeConfiguration<FocusSession>
{
    public void Configure(EntityTypeBuilder<FocusSession> builder)
    {
        builder.ToTable("FocusSessions");
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(s => new { s.UserId, s.StartedAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class SubtaskConfiguration : IEntityTypeConfiguration<Subtask>
{
    public void Configure(EntityTypeBuilder<Subtask> builder)
    {
        builder.ToTable("Subtasks");
        builder.Property(s => s.Title).HasMaxLength(300);
        builder.HasIndex(s => s.TaskId);
    }
}
