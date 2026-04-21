using System.Globalization;
using System.Text;
using IsoDoc.Application.Compliance.Queries.GetApprovalSlaReport;
using IsoDoc.Application.Compliance.Queries.GetDocumentStatusReport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Controllers;

[Route("api/v1/reports")]
[Authorize]
public sealed class ReportsController : ApiControllerBase
{
    [HttpGet("document-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DocumentStatus(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDocumentStatusReportQuery(), ct);
        return FromResult(result);
    }

    [HttpGet("document-status/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportDocumentStatus(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDocumentStatusReportQuery(), ct);
        if (!result.IsSuccess)
            return FromResult(result);

        var sb = new StringBuilder();
        sb.AppendLine("Status,Count");
        foreach (var row in result.Value!)
            sb.AppendLine($"{CsvEscape(row.Status)},{row.Count.ToString(CultureInfo.InvariantCulture)}");

        return CsvFile(sb.ToString(), "document-status.csv");
    }

    [HttpGet("approval-sla")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApprovalSla(
        [FromQuery] DateTime? completedFromUtc = null,
        [FromQuery] DateTime? completedToUtc = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetApprovalSlaReportQuery
            {
                CompletedFromUtc = completedFromUtc,
                CompletedToUtc = completedToUtc
            },
            ct);

        return FromResult(result);
    }

    [HttpGet("approval-sla/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportApprovalSla(
        [FromQuery] DateTime? completedFromUtc = null,
        [FromQuery] DateTime? completedToUtc = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetApprovalSlaReportQuery
            {
                CompletedFromUtc = completedFromUtc,
                CompletedToUtc = completedToUtc
            },
            ct);

        if (!result.IsSuccess)
            return FromResult(result);

        var sb = new StringBuilder();
        sb.AppendLine(
            "WorkflowId,DocumentId,DocumentCode,DocumentTitle,StartedAtUtc,CompletedAtUtc,WorkflowStatus,ClosedCycleHours,OpenWaitingHours");

        foreach (var r in result.Value!)
        {
            sb.AppendLine(string.Join(',',
                r.WorkflowId.ToString(),
                r.DocumentId.ToString(),
                CsvEscape(r.DocumentCode),
                CsvEscape(r.DocumentTitle),
                r.StartedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                r.CompletedAtUtc?.ToString("o", CultureInfo.InvariantCulture) ?? "",
                CsvEscape(r.WorkflowStatus),
                r.ClosedCycleHours?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.OpenWaitingHours?.ToString(CultureInfo.InvariantCulture) ?? ""));
        }

        return CsvFile(sb.ToString(), "approval-sla.csv");
    }

    private static FileContentResult CsvFile(string content, string downloadName)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return new FileContentResult(bytes, "text/csv; charset=utf-8") { FileDownloadName = downloadName };
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

        return value;
    }
}
