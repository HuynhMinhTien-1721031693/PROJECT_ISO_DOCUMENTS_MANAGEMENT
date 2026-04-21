using System.Net.Http.Headers;
using IsoDoc.Blazor.Models;
using IsoDoc.Blazor.Services.Auth;

namespace IsoDoc.Blazor.Services.Api;

public sealed class DocumentApiService
{
    private readonly ApiClient _apiClient;
    private readonly HttpClient _httpClient;
    private readonly TokenStorageService _tokenStorage;

    public DocumentApiService(ApiClient apiClient, HttpClient httpClient, TokenStorageService tokenStorage)
    {
        _apiClient = apiClient;
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public async Task<(IReadOnlyList<DocumentSummaryDto> Items, PaginationMeta? Pagination, string? Error)> SearchAsync(
        SearchDocumentsParams request,
        CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}",
            $"sortBy={Uri.EscapeDataString(request.SortBy)}",
            $"sortDesc={request.SortDesc.ToString().ToLowerInvariant()}"
        };

        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query.Add($"keyword={Uri.EscapeDataString(request.Keyword)}");
        if (!string.IsNullOrWhiteSpace(request.Status))
            query.Add($"status={Uri.EscapeDataString(request.Status)}");
        if (!string.IsNullOrWhiteSpace(request.Category))
            query.Add($"category={Uri.EscapeDataString(request.Category)}");
        if (!string.IsNullOrWhiteSpace(request.Standard))
            query.Add($"standard={Uri.EscapeDataString(request.Standard)}");

        var (data, pagination, error) = await _apiClient.GetWrappedAsync<IReadOnlyList<DocumentSummaryDto>>(
            $"Documents?{string.Join("&", query)}",
            ct);

        return (data ?? Array.Empty<DocumentSummaryDto>(), pagination, FormatError(error));
    }

    public async Task<(DocumentDto? Document, string? Error)> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var (data, _, error) = await _apiClient.GetWrappedAsync<DocumentDto>($"Documents/{id}", ct);
        return (data, FormatError(error));
    }

    public async Task<(Guid? Id, string? Error)> UploadAsync(
        string title,
        string documentCode,
        string standard,
        string category,
        string? description,
        string? changeNote,
        IReadOnlyList<string> tags,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(title), "title");
        formData.Add(new StringContent(documentCode), "documentCode");
        formData.Add(new StringContent(standard), "standard");
        formData.Add(new StringContent(category), "category");
        if (!string.IsNullOrWhiteSpace(description))
            formData.Add(new StringContent(description), "description");
        if (!string.IsNullOrWhiteSpace(changeNote))
            formData.Add(new StringContent(changeNote), "changeNote");

        foreach (var tag in tags.Where(x => !string.IsNullOrWhiteSpace(x)))
            formData.Add(new StringContent(tag.Trim()), "tags");

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        formData.Add(fileContent, "file", fileName);

        // Upload endpoint returns 201 with wrapped Guid data.
        var token = (await _tokenStorage.GetTokensAsync())?.AccessToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, "Documents") { Content = formData };
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return (null, ApiProblemMessageFormatter.Format(await response.Content.ReadAsStringAsync(ct)));

        var wrapped = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>(cancellationToken: ct);
        return (wrapped?.Data, wrapped?.Data is null ? "Phản hồi upload không hợp lệ." : null);
    }

    public async Task<(DocumentDownloadInfoDto? Info, string? Error)> GetDownloadInfoAsync(
        Guid documentId,
        Guid? versionId = null,
        CancellationToken ct = default)
    {
        var qs = versionId is null || versionId == Guid.Empty ? "" : $"?versionId={versionId}";
        var (data, _, error) = await _apiClient.GetWrappedAsync<DocumentDownloadInfoDto>($"Documents/{documentId}/download{qs}", ct);
        return (data, FormatError(error));
    }

    public async Task<(Guid? VersionId, string? Error)> AddVersionAsync(
        Guid documentId,
        Stream fileStream,
        string fileName,
        string contentType,
        string? changeNote,
        CancellationToken ct = default)
    {
        using var formData = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(changeNote))
            formData.Add(new StringContent(changeNote), "changeNote");

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        formData.Add(fileContent, "file", fileName);

        var token = (await _tokenStorage.GetTokensAsync())?.AccessToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"Documents/{documentId}/versions") { Content = formData };
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return (null, ApiProblemMessageFormatter.Format(await response.Content.ReadAsStringAsync(ct)));

        var wrapped = await response.Content.ReadFromJsonAsync<ApiResponse<DocumentVersionCreatedDto>>(cancellationToken: ct);
        return (wrapped?.Data?.VersionId, wrapped?.Data?.VersionId is null ? "Phản hồi tạo phiên bản không hợp lệ." : null);
    }

    public async Task<(bool Ok, string? Error)> UpdateMetadataAsync(
        Guid documentId,
        string? title,
        string? description,
        IReadOnlyList<string>? tags,
        CancellationToken ct = default)
    {
        var (ok, err) = await _apiClient.PutJsonAsync(
            $"Documents/{documentId}",
            new { title, description, tags },
            ct);

        return (ok, FormatError(err));
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid documentId, CancellationToken ct = default)
    {
        var (ok, err) = await _apiClient.DeleteAsync($"Documents/{documentId}", ct);
        return (ok, FormatError(err));
    }

    public async Task<(Guid? WorkflowId, string? Error)> SubmitForApprovalAsync(Guid documentId, CancellationToken ct = default)
    {
        var (data, error) = await _apiClient.PostWrappedEmptyAsync<SubmitWorkflowResponseDto>($"Documents/{documentId}/submit", ct);
        return (data?.WorkflowId, FormatError(error));
    }

    public string GetAuthorizedFileUrl(Guid documentId, Guid? versionId = null)
    {
        var qs = versionId is null || versionId == Guid.Empty ? "" : $"?versionId={versionId}";
        var root = _httpClient.BaseAddress!.ToString().TrimEnd('/');
        return $"{root}/Documents/{documentId}/file{qs}";
    }

    private static string? FormatError(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : ApiProblemMessageFormatter.Format(raw);
}
