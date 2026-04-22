namespace IsoDoc.Blazor.Models;

public sealed class ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public PaginationMeta? Pagination { get; init; }
}

public sealed class PaginationMeta
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrev { get; init; }
}
