using System.Globalization;
using System.Text;
using IsoDoc.Application.Audit.Queries.SearchAuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public sealed class AuditLogsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = BuildQuery(userId, action, entityType, entityId, fromUtc, toUtc, page, pageSize);
        var result = await Mediator.Send(query, ct);
        return result.IsSuccess ? PagedResult(result.Value!) : FromResult(result);
    }

    /// <summary>CSV export (UTF-8 BOM) for ISO internal evidence — tối đa 1000 dòng theo bộ lọc hiện tại.</summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        var query = BuildQuery(userId, action, entityType, entityId, fromUtc, toUtc, page: 1, pageSize: 1000);
        var result = await Mediator.Send(query, ct);
        if (!result.IsSuccess)
            return FromResult(result);

        var sb = new StringBuilder();
        sb.AppendLine("Id,UserId,Action,EntityType,EntityId,IpAddress,OccurredAtUtc");
        foreach (var r in result.Value!.Items)
        {
            sb.AppendLine(string.Join(',',
                r.Id.ToString(),
                r.UserId?.ToString() ?? "",
                CsvEscape(r.Action),
                CsvEscape(r.EntityType),
                CsvEscape(r.EntityId),
                CsvEscape(r.IpAddress),
                r.OccurredAtUtc.ToString("o", CultureInfo.InvariantCulture)));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return new FileContentResult(bytes, "text/csv; charset=utf-8") { FileDownloadName = "audit-log.csv" };
    }

    private static SearchAuditLogsQuery BuildQuery(
        Guid? userId,
        string? action,
        string? entityType,
        string? entityId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize)
        => new()
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = page,
            PageSize = pageSize
        };

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

        return value;
    }
}
