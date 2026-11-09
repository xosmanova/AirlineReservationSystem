namespace AirlineReservation.Application.Common.Models;

/// <summary>
/// Generic paginated result container containing items and metadata.
/// </summary>
/// <typeparam name="T">Type of the items in the paginated list</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}
