using IsoDoc.Application.Documents.Commands.SubmitForApproval;
using IsoDoc.Application.Documents.Commands.UploadDocument;
using IsoDoc.Application.Documents.Queries.GetDocumentById;
using IsoDoc.Application.Documents.Queries.SearchDocuments;
using IsoDoc.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public sealed class DocumentsController : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword = null,
        [FromQuery] IsoStandard? standard = null,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] DocumentCategory? category = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "UpdatedAt",
        [FromQuery] bool sortDesc = true,
        CancellationToken ct = default)
    {
        var query = new SearchDocumentsQuery
        {
            Keyword = keyword,
            Standard = standard,
            Status = status,
            Category = category,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDesc = sortDesc
        };

        var result = await Mediator.Send(query, ct);
        return result.IsSuccess ? PagedResult(result.Value!) : FromResult(result);
    }

    [HttpGet("{id:guid}", Name = "GetDocumentById")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDocumentByIdQuery(id), ct);
        return FromResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "RequireController")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(Problem("File khong duoc rong."));

        await using var readStream = request.File.OpenReadStream();
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var checksumHex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

        var cmd = new UploadDocumentCommand
        {
            Title = request.Title,
            DocumentCode = request.DocumentCode.Trim(),
            Standard = request.Standard,
            Category = request.Category,
            Description = request.Description,
            Tags = request.Tags ?? new List<string>(),
            FileStream = new MemoryStream(bytes),
            FileName = request.File.FileName,
            ContentType = request.File.ContentType ?? "application/octet-stream",
            FileSize = bytes.LongLength,
            ChecksumHex = checksumHex,
            ChangeNote = request.ChangeNote
        };

        var result = await Mediator.Send(cmd, ct);
        return result.IsSuccess
            ? CreatedResult(result.Value, "GetDocumentById", new { id = result.Value })
            : FromResult(result);
    }

    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDownloadUrl(Guid id, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return OkResult(new { downloadUrl = $"https://blob.azure.com/iso-documents/{id}/file.pdf?sas=..." });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "RequireController")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new SubmitForApprovalCommand(id), ct);
        return result.IsSuccess ? OkResult(new { workflowId = result.Value }) : FromResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireISOManager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return NoContent();
    }

    private static ProblemDetails Problem(string detail) => new() { Detail = detail };
}

public sealed class UploadDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public IsoStandard Standard { get; set; }
    public DocumentCategory Category { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public IFormFile? File { get; set; }
    public string? ChangeNote { get; set; }
}

public sealed class AddVersionRequest
{
    public IFormFile? File { get; set; }
    public string? ChangeNote { get; set; }
}
