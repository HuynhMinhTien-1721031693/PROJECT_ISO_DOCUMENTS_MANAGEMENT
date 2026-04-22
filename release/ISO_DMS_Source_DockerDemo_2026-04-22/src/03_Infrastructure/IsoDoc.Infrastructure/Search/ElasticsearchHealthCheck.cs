using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Search;

/// <summary>Optional readiness signal for Elasticsearch (does not fail overall liveness when degraded).</summary>
public sealed class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchHealthCheck> _logger;

    public ElasticsearchHealthCheck(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchHealthCheck> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ping = await _client.PingAsync(cancellationToken);
            if (!ping.IsSuccess())
            {
                _logger.LogWarning(
                    "Elasticsearch ping to {Uri} failed for index {Index}.",
                    _options.Uri,
                    _options.IndexName);
                return HealthCheckResult.Degraded("Elasticsearch ping failed.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch health check failed.");
            return HealthCheckResult.Degraded("Elasticsearch unavailable.", ex);
        }
    }
}
