namespace KotoDibo.Application.Common;

// Generic paged-list envelope. Introduced for the expense listing endpoint, the first in this
// codebase to need real pagination (personal expense history can grow into the tens of thousands
// of rows) — reusable by any future list endpoint facing the same growth.
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages { get; init; }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, long totalCount) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount,
        TotalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0,
    };
}
