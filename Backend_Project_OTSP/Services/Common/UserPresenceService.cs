using System.Collections.Concurrent;

namespace Backend_Project_OTSP.Services.Common;

public interface IUserPresenceService
{
    bool OnConnected(string userId, string connectionId);
    bool OnDisconnected(string? userId, string connectionId, out string? resolvedUserId);
    void OnDisconnected(string? userId, string connectionId);
    void TouchUserActivity(string userId);
    void MarkOffline(string userId);
    bool IsUserOnline(string? userId);
}

public sealed class UserPresenceService : IUserPresenceService
{
    private static readonly TimeSpan ActivityTtl = TimeSpan.FromMinutes(3);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _userConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _connectionUserMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastSeenUtc = new(StringComparer.OrdinalIgnoreCase);

    public bool OnConnected(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId)) return false;

        _connectionUserMap[connectionId] = userId;
        var set = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
        var wasEmpty = set.IsEmpty;
        set[connectionId] = 1;
        _lastSeenUtc[userId] = DateTime.UtcNow;

        return wasEmpty;
    }

    public bool OnDisconnected(string? userId, string connectionId, out string? resolvedUserId)
    {
        resolvedUserId = userId;
        if (string.IsNullOrWhiteSpace(resolvedUserId))
        {
            _connectionUserMap.TryGetValue(connectionId, out resolvedUserId);
        }
        _connectionUserMap.TryRemove(connectionId, out _);

        if (!string.IsNullOrWhiteSpace(resolvedUserId) && _userConnections.TryGetValue(resolvedUserId, out var set))
        {
            set.TryRemove(connectionId, out _);
            if (set.IsEmpty)
            {
                _userConnections.TryRemove(resolvedUserId, out _);
                _lastSeenUtc.TryRemove(resolvedUserId, out _);
                return true;
            }
        }
        return false;
    }

    public void OnDisconnected(string? userId, string connectionId)
    {
        OnDisconnected(userId, connectionId, out _);
    }

    public void TouchUserActivity(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        _lastSeenUtc[userId] = DateTime.UtcNow;
    }

    public void MarkOffline(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        if (_userConnections.TryRemove(userId, out var conns))
        {
            foreach (var connId in conns.Keys)
            {
                _connectionUserMap.TryRemove(connId, out _);
            }
        }
        _lastSeenUtc.TryRemove(userId, out _);
    }

    public bool IsUserOnline(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        // 1. Phải có kết nối SignalR đang mở (đang mở tab ứng dụng OTSP)
        if (!_userConnections.TryGetValue(userId, out var connections) || connections.IsEmpty)
        {
            return false;
        }

        // 2. VÀ phải có hoạt động trong vòng 3 phút (inactivity > 3 phút => Offline)
        if (_lastSeenUtc.TryGetValue(userId, out var lastSeen))
        {
            return (DateTime.UtcNow - lastSeen) <= ActivityTtl;
        }

        return false;
    }
}

