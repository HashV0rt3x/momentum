using FluentValidation;

namespace Momentum.Application.Categories;

/// <summary>Spec section 5. Key is a stable machine key the frontend localizes.</summary>
public sealed record CategoryDto(string Id, string Key, string Color, string Icon);

public sealed record CreateCategoryRequest(string Key, string Color, string Icon);

/// <summary>PATCH — nulls mean "leave unchanged". Key is immutable by design.</summary>
public sealed record UpdateCategoryRequest(string? Color, string? Icon);

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .Matches("^[a-z0-9_]{1,64}$")
            .WithMessage("Key must be lowercase letters, digits, or underscores (max 64).");

        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9A-Fa-f]{6}$");
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(64);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.Color is not null);
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(64).When(x => x.Icon is not null);
    }
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken);
}
