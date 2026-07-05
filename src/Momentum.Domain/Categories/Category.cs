using Momentum.Domain.Common;

namespace Momentum.Domain.Categories;

/// <summary>
/// Per-user activity category (spec section 5). The API never translates
/// names — <see cref="Key"/> is a stable machine key (e.g. "deep_work") that
/// the frontend localizes. Unique per user.
/// </summary>
public sealed class Category : UserOwnedEntity
{
    private Category()
    {
    }

    public Category(Guid userId, string key, string color, string icon) : base(userId)
    {
        Key = key;
        Color = color;
        Icon = icon;
    }

    public string Key { get; set; } = string.Empty;

    /// <summary>Hex color, e.g. "#3E63C8".</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Lucide icon name, e.g. "Brain".</summary>
    public string Icon { get; set; } = string.Empty;
}
