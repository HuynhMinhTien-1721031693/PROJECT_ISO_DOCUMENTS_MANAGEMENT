using IsoDoc.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Models
{
public sealed class ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public PaginationMeta? Pagination { get; init; }

    public static ApiResponse<T> Ok(T data, PaginationMeta? pagination = null)
        => new() { IsSuccess = true, Data = data, Pagination = pagination };
}

public sealed class PaginationMeta
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrev { get; init; }

    public static PaginationMeta From<T>(PagedList<T> paged)
        => new()
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            HasNext = paged.HasNextPage,
            HasPrev = paged.HasPreviousPage
        };
}
}

namespace IsoDoc.WebAPI.Controllers
{
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult OkResult<T>(T data) =>
        Ok(IsoDoc.WebAPI.Models.ApiResponse<T>.Ok(data));

    protected IActionResult PagedResult<T>(PagedList<T> paged) =>
        Ok(IsoDoc.WebAPI.Models.ApiResponse<IReadOnlyList<T>>.Ok(
            paged.Items,
            IsoDoc.WebAPI.Models.PaginationMeta.From(paged)));

    protected IActionResult CreatedResult<T>(T data, string routeName, object routeValues) =>
        CreatedAtRoute(routeName, routeValues, IsoDoc.WebAPI.Models.ApiResponse<T>.Ok(data));

    protected IActionResult FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(IsoDoc.WebAPI.Models.ApiResponse<T>.Ok(result.Value!));

        return result.ErrorCode switch
        {
            "DOCUMENT_NOT_FOUND" or "WORKFLOW_NOT_FOUND" => NotFound(Problem(result.Error)),
            "DOCUMENT_CODE_DUPLICATE" => Conflict(Problem(result.Error)),
            "UNAUTHORIZED" => Forbid(),
            _ => UnprocessableEntity(Problem(result.Error))
        };
    }

    protected IActionResult FromResult(Result result)
    {
        if (result.IsSuccess)
            return NoContent();
        return UnprocessableEntity(Problem(result.Error));
    }

    private static ProblemDetails Problem(string? detail) => new()
    {
        Detail = detail,
        Status = StatusCodes.Status422UnprocessableEntity
    };
}
}
