using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
namespace IsoDoc.Integration.Tests;

public sealed class IsoDocWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        foreach (var (key, value) in TestConfiguration)
            builder.UseSetting(key, value);
    }

    /// <summary>UseSetting wins over appsettings.* so we stay on in-memory repos + config-file auth.</summary>
    private static readonly IReadOnlyList<KeyValuePair<string, string>> TestConfiguration =
        new List<KeyValuePair<string, string>>
        {
            new("ConnectionStrings:SqlServer", ""),
            new("ConnectionStrings:DefaultConnection", ""),
            new("ConnectionStrings:Redis", ""),
            new("IsoDoc:Elasticsearch:Uri", "http://127.0.0.1:1"),
            new("IsoDoc:Elasticsearch:ParticipateInHealthChecks", "false"),
            new("Approvers:QaOfficerId", "11111111-1111-1111-1111-111111111111"),
            new("Approvers:SafetyOfficerId", "11111111-1111-1111-1111-111111111111"),
            new("Approvers:IsmsOfficerId", "11111111-1111-1111-1111-111111111111"),
            new("Approvers:IsoManagerId", "11111111-1111-1111-1111-111111111111"),
            new("Auth:AllowPlainTextForDev", "true"),
            new("Auth:Users:0:UserId", "11111111-1111-1111-1111-111111111111"),
            new("Auth:Users:0:Email", "integration@test.local"),
            new("Auth:Users:0:Password", "Test@12345"),
            new("Auth:Users:0:DepartmentId", "22222222-2222-2222-2222-222222222222"),
            new("Auth:Users:0:Roles:0", "SystemAdmin"),
            new("Auth:Users:0:Roles:1", "DocumentController"),
            new("IsoDoc:Jwt:Issuer", "IsoDoc"),
            new("IsoDoc:Jwt:Audience", "IsoDoc.Api"),
            new("IsoDoc:Jwt:SigningKey", "TEST_INTEGRATION_SIGNING_KEY_32_CHARS!!"),
        };
}
