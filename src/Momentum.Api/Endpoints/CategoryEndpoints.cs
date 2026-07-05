using Momentum.Api.Validation;
using Momentum.Application.Categories;
using Momentum.Application.Common.Security;

namespace Momentum.Api.Endpoints;

/// <summary>Spec section 5 — Categories (stable keys, frontend localizes names).</summary>
public static class CategoryEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/categories").WithTags("Categories").RequireAuthorization();

        group.MapGet("", async (
                ICurrentUser currentUser,
                ICategoryService categoryService,
                CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.ListAsync(currentUser.UserId, cancellationToken)));

        group.MapPost("", async (
                CreateCategoryRequest request,
                ICurrentUser currentUser,
                ICategoryService categoryService,
                CancellationToken cancellationToken) =>
            {
                var category = await categoryService.CreateAsync(currentUser.UserId, request, cancellationToken);
                return Results.Created($"/api/v1/categories/{category.Id}", category);
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateCategoryRequest>>();

        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateCategoryRequest request,
                ICurrentUser currentUser,
                ICategoryService categoryService,
                CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.UpdateAsync(currentUser.UserId, id, request, cancellationToken)))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateCategoryRequest>>();

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ICurrentUser currentUser,
                ICategoryService categoryService,
                CancellationToken cancellationToken) =>
            {
                await categoryService.DeleteAsync(currentUser.UserId, id, cancellationToken);
                return Results.NoContent();
            });
    }
}
