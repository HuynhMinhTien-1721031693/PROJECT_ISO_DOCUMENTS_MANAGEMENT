using System.Collections.Concurrent;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Infrastructure.Identity;

namespace IsoDoc.Infrastructure.InMemory;

public sealed class InMemoryCacheService : ICacheService, IRedisCacheService
{
    private sealed record CacheEntry(object Value, DateTimeOffset? ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, CacheEntry> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAtUtc is not null && entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _store.TryRemove(key, out _);
                return Task.FromResult<T?>(null);
            }

            return Task.FromResult(entry.Value as T);
        }

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration, CancellationToken ct = default) where T : class
    {
        DateTimeOffset? expiresAt = absoluteExpiration is null
            ? null
            : DateTimeOffset.UtcNow.Add(absoluteExpiration.Value);
        _store[key] = new CacheEntry(value, expiresAt);
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class
    {
        _store[key] = new CacheEntry(value, DateTimeOffset.UtcNow.Add(expiry));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken ct = default)
    {
        var keys = _store.Keys.Where(k => k.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var key in keys)
            _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
