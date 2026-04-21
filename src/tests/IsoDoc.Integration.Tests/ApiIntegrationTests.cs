using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IsoDoc.Integration.Tests;

public sealed class ApiIntegrationTests : IClassFixture<IsoDocWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(IsoDocWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    [Fact]
    public async Task Login_invalid_credentials_returns_401()
    {
        var res = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "integration@test.local", password = "WrongPassword!" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Documents_get_without_token_returns_401()
    {
        var res = await _client.GetAsync("/api/v1/documents?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_upload_submit_workflow_and_search_fallback_succeed()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "integration@test.local", password = "Test@12345" });
        login.EnsureSuccessStatusCode();
        var token = await ReadAccessTokenAsync(login);
        Assert.False(string.IsNullOrWhiteSpace(token));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var code = $"QMS-PR-{Random.Shared.Next(100, 999)}";
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n");
        var checksum = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Integration title"), "Title");
        form.Add(new StringContent(code), "DocumentCode");
        form.Add(new StringContent("1"), "Standard");
        form.Add(new StringContent("1"), "Category");
        form.Add(CreatePdfFileContent(pdfBytes), "File", "test.pdf");

        var upload = await _client.PostAsync("/api/v1/documents", form);
        upload.EnsureSuccessStatusCode();
        var docId = await ReadDataGuidAsync(upload);

        var submit = await _client.PostAsync($"/api/v1/documents/{docId}/submit", null);
        submit.EnsureSuccessStatusCode();
        var workflowId = await ReadNestedGuidAsync(submit, "workflowId");

        var decisionBody = JsonContent.Create(new { decision = "Approved", comment = "ok" });
        var decision = await _client.PostAsync($"/api/v1/workflow/{workflowId}/decision", decisionBody);
        Assert.Equal(HttpStatusCode.NoContent, decision.StatusCode);

        var decision2 = await _client.PostAsync($"/api/v1/workflow/{workflowId}/decision", decisionBody);
        Assert.Equal(HttpStatusCode.NoContent, decision2.StatusCode);

        var search = await _client.GetAsync("/api/v1/search?q=Integration&page=1&pageSize=10");
        search.EnsureSuccessStatusCode();
        var searchJson = await search.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(searchJson);
        Assert.True(doc.RootElement.GetProperty("isSuccess").GetBoolean());
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.NotEmpty(data.EnumerateArray());
    }

    private static ByteArrayContent CreatePdfFileContent(byte[] pdfBytes)
    {
        var file = new ByteArrayContent(pdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return file;
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage login)
    {
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString() ?? "";
    }

    private static async Task<Guid> ReadDataGuidAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = doc.RootElement.GetProperty("data").GetString();
        Assert.False(string.IsNullOrWhiteSpace(text));
        return Guid.Parse(text!);
    }

    private static async Task<Guid> ReadNestedGuidAsync(HttpResponseMessage response, string property)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty(property).GetGuid();
    }
}
