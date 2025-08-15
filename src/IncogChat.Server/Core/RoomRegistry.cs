namespace IncogChat.Server.Core;

using System.Collections.Concurrent;

public sealed class RoomRegistry
{
    private readonly ConcurrentDictionary<string, RoomState> _rooms = new();

    public bool Exists(string passcode) => _rooms.ContainsKey(passcode);

    public RoomState CreateRoom(string passcode)
    {
        var now = DateTimeOffset.UtcNow;
        var room = new RoomState { Passcode = passcode, LastActivityUtc = now };
        if (!_rooms.TryAdd(passcode, room))
            throw new InvalidOperationException("Room collision.");
        return room;
    }

    public RoomState GetOrThrow(string passcode)
    {
        if (_rooms.TryGetValue(passcode, out var room)) return room;
        throw new InvalidOperationException("Room not found.");
    }

    public bool TryGet(string passcode, out RoomState? room) => _rooms.TryGetValue(passcode, out room);

    public void RemoveRoom(string passcode) => _rooms.TryRemove(passcode, out _);

    public IEnumerable<RoomState> AllRooms() => _rooms.Values;
}
