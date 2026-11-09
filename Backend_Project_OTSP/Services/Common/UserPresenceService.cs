using System.Collections.Concurrent;

namespace Backend_Project_OTSP.Services.Common;

public interface IUserPresenceService
{
    void OnConnected(string userId, string connectionId);
    void OnDisconnected(string? userId, string connectionId);
    void TouchUserActivity(string userId);
    void MarkOffline(string userId);
    bool IsUserOnline(string? userId);
}

public sealed class UserPresenceService : IUserPresenceService
{
    private static readonly TimeSpan ActivityTtl = TimeSpan.FromMinutes(3);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _userConnections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _lastSeenUtc = new(StringComparer.Ordinal);

    public void OnConnected(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId)) return;
        var set = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        set[connectionId] = 1;
        _lastSeenUtc[userId] = DateTime.UtcNow;
    }

    public void OnDisconnected(string? userId, string connectionId)
    {
        if (!string.IsNullOrWhiteSpace(userId) && _userConnections.TryGetValue(userId, out var set))
        {
            set.TryRemove(connectionId, out _);
            if (set.IsEmpty)
            {
                _userConnections.TryRemove(userId, out _);
            }
        }
    }

    public void TouchUserActivity(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        _lastSeenUtc[userId] = DateTime.UtcNow;
    }

    public void MarkOffline(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        _userConnections.TryRemove(userId, out _);
        _lastSeenUtc.TryRemove(userId, out _);
    }

    public bool IsUserOnline(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        if (_userConnections.TryGetValue(userId, out var connections) && !connections.IsEmpty)
        {
            return true;
        }

        if (_lastSeenUtc.TryGetValue(userId, out var lastSeen))
        {
            return (DateTime.UtcNow - lastSeen) <= ActivityTtl;
        }

        return false;
    }
}

