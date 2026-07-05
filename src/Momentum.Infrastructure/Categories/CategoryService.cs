using Microsoft.EntityFrameworkCore;
using Momentum.Application.Categories;
using Momentum.Application.Common.Exceptions;
using Momentum.Domain.Categories;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Categories;

public sealed class CategoryService(AppDbContext dbContext) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
        => await dbContext.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new CategoryDto(c.Id.ToString(), c.Key, c.Color, c.Icon))
            .ToListAsync(cancellationToken);

    public async Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Categories
            .AnyAsync(c => c.UserId == userId && c.Key == request.Key, cancellationToken);

        if (exists)
        {
            throw new ConflictException($"Category with key '{request.Key}' already exists.");
        }

        var category = new Category(userId, request.Key, request.Color, request.Icon);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id.ToString(), category.Key, category.Color, category.Icon);
    }

    public async Task<CategoryDto> UpdateAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await FindAsync(userId, categoryId, cancellationToken);

        category.Color = request.Color ?? category.Color;
        category.Icon = request.Icon ?? category.Icon;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id.ToString(), category.Key, category.Color, category.Icon);
    }

    public async Task DeleteAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await FindAsync(userId, categoryId, cancellationToken);
        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> FindAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken)
        => await dbContext.Categories
               .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == categoryId, cancellationToken)
           ?? throw new NotFoundException("Category", categoryId);
}
