using System.ComponentModel.DataAnnotations;

namespace RentApp.Application.Common.Paging;

/// <summary>Query-string paging shared by list endpoints: ?page=1&amp;pageSize=20.</summary>
public class PageQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or more.")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; init; } = 20;

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
