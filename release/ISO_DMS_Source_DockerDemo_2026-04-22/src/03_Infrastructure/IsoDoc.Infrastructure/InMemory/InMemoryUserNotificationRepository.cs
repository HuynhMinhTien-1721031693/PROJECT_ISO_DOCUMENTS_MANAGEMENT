using System.Collections.Concurrent;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.InMemory;

public sealed class InMemoryUserNotificationRepository : IUserNotificationRepository
{
    private readonly ConcurrentDictionary<Guid, UserNotification> _items = new();

    public Task<UserNotification> AddAsync(UserNotification notification, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        notification.CreatedAt = now;
        notification.UpdatedAt = now;
        _items[notification.Id] = notification;
        return Task.FromResult(notification);
    }

    public Task<(IReadOnlyList<UserNotification> Items, int TotalCount)> GetPageForUserAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _items.Values.Where(n => n.UserId == userId);
        if (unreadOnly)
            q = q.Where(n => !n.IsRead);

        var list = q.OrderByDescending(n => n.CreatedAt).ToList();
        var total = list.Count;
        var pageItems = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(((IReadOnlyList<UserNotification>)pageItems, total));
    }

    public Task<UserNotification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return Task.FromResult(_items.TryGetValue(id, out var n) && n.UserId == userId ? n : null);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_items.Values.Count(n => n.UserId == userId && !n.IsRead));

    public Task MarkReadAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        if (_items.TryGetValue(id, out var n) && n.UserId == userId)
            n.IsRead = true;
        return Task.CompletedTask;
    }

    public Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        foreach (var n in _items.Values.Where(x => x.UserId == userId && !x.IsRead))
            n.IsRead = true;
        return Task.CompletedTask;
    }
}
