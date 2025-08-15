namespace IncogChat.Server.Core;

using System.Collections.Concurrent;

public sealed class Participant
{
    public required string ConnectionId { get; init; }
    public required string DisplayName { get; init; }
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class RoomState
{
    public required string Passcode { get; init; } // "########"
    public string? OwnerConnectionId { get; set; }
    public DateTimeOffset LastActivityUtc { get; set; }
    public ConcurrentDictionary<string, Participant> Participants { get; } = new();
}

public static class ServerConfig
{
    public static readonly TimeSpan UserIdle = TimeSpan.FromMinutes(
        double.TryParse(Environment.GetEnvironmentVariable("USER_IDLE_MINUTES"), out var m) ? m : 5);

    public static readonly TimeSpan RoomTtl = TimeSpan.FromMinutes(
        double.TryParse(Environment.GetEnvironmentVariable("ROOM_TTL_MINUTES"), out var n) ? n : 10);
}
