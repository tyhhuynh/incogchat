namespace IncogChat.Server.Infra;

using System.Collections.Concurrent;

public sealed class ActionRateLimiter
{
    private record Key(string Ip, string Action);
    private readonly ConcurrentDictionary<Key, Queue<DateTimeOffset>> _hits = new();

    public bool TryConsume(string ip, string action, int limit, TimeSpan window)
    {
        var key = new Key(ip, action);
        var now = DateTimeOffset.UtcNow;
        var q = _hits.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() > window) q.Dequeue();
            if (q.Count >= limit) return false;
            q.Enqueue(now);
            return true;
        }
    }
}
