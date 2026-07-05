using System.Text.Json.Serialization;

namespace Momentum.Application.Common;

/// <summary>
/// Paged list response. Serialized property names match the API spec:
/// { "items": [...], "total": ..., "page": ..., "pageSize": ... }.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    [JsonPropertyName("total")]
    public required int TotalCount { get; init; }

    [JsonIgnore]
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount,
    };
}
