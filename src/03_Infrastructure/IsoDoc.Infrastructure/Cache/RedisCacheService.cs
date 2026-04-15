using System.Text.Json;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace IsoDoc.Infrastructure.Cache
{
public sealed class RedisCacheService : ICacheService, IRedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (!value.HasValue) return null;
            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration, CancellationToken ct = default) where T : class
    {
        var expiry = absoluteExpiration ?? TimeSpan.FromMinutes(5);
        await SetAsync(key, value, expiry, ct);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DELETE failed for key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string keyPrefix, CancellationToken ct = default)
    {
        try
        {
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected)
                    continue;

                await foreach (var key in server.KeysAsync(pattern: keyPrefix + "*"))
                    await _redis.GetDatabase().KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DELETE by prefix failed for {Prefix}", keyPrefix);
        }
    }
}
}

namespace IsoDoc.Infrastructure.Notifications
{
public sealed class NotificationService : INotificationSender
{
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IOptions<NotificationOptions> options,
        ILogger<NotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Sending email to {ToEmail} with subject {Subject} via host {Host}",
            toEmail, subject, _options.SmtpHost);
        return Task.CompletedTask;
    }

    public Task SendInAppNotificationAsync(
        Guid userId,
        string title,
        string message,
        string? actionUrl = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("In-app notification to {UserId}: {Title}", userId, title);
        return Task.CompletedTask;
    }
}

public sealed class NotificationOptions
{
    public const string Section = "Notifications";
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@isodms.internal";
    public string FromName { get; set; } = "ISO Document System";
}
}
