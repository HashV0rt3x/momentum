namespace Momentum.Application.Common;

/// <summary>Simple offset paging request. Bind from query string in endpoints.</summary>
public sealed class PagedRequest
{
    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value,
        };
    }

    public int Skip => (Page - 1) * PageSize;
}
