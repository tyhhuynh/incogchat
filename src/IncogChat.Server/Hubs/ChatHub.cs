namespace IncogChat.Server.Hubs;

using Microsoft.AspNetCore.SignalR;
using Serilog;
using IncogChat.Server.Core;
using IncogChat.Server.Infra;

public sealed class ChatHub : Hub
{
    private readonly RoomRegistry _reg;
    private readonly ActionRateLimiter _limiter;

    public ChatHub(RoomRegistry reg, ActionRateLimiter limiter)
    {
        _reg = reg; _limiter = limiter;
    }

    private static string Ip(HttpContext? ctx) =>
        ctx?.Request.Headers["X-Forwarded-For"].FirstOrDefault()
        ?? ctx?.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    public async Task<string> CreateRoom(string? displayName = null)
    {
        var connectionId = Context.ConnectionId;
        if (string.IsNullOrEmpty(connectionId))
            throw new HubException("Invalid connection state.");

        var ctx = Context.GetHttpContext();
        var ip = Ip(ctx);
        if (!_limiter.TryConsume(ip, "create", 3, TimeSpan.FromMinutes(1)))
            throw new HubException("Rate limit exceeded.");

        var code = Passcode.GenerateUnique(_reg);
        var room = _reg.CreateRoom(code);
        room.OwnerConnectionId = connectionId;

        await Groups.AddToGroupAsync(connectionId, code);

        // Use provided display name or generate default
        var hostName = !string.IsNullOrWhiteSpace(displayName) 
            ? Validation.ValidateName(displayName)
            : $"Host-{connectionId[..5]}";

        room.Participants[connectionId] = new Participant
        {
            ConnectionId = connectionId,
            DisplayName = hostName,
            LastSeenUtc = DateTimeOffset.UtcNow
        };
        room.LastActivityUtc = DateTimeOffset.UtcNow;

        Log.Information("Room created via Hub with host name: {HostName}.", hostName);
        return code;
    }

    public sealed class JoinResult
    {
        public bool Success { get; set; }
        public bool IsOwner { get; set; }
        public required string NormalizedDisplayName { get; set; }
    }

    public async Task<JoinResult> JoinRoom(string passcode, string displayName)
    {
        var connectionId = Context.ConnectionId;
        if (string.IsNullOrEmpty(connectionId))
            throw new HubException("Invalid connection state.");

        var ctx = Context.GetHttpContext();
        var ip = Ip(ctx);
        if (!_limiter.TryConsume(ip, "join", 10, TimeSpan.FromMinutes(1)))
            throw new HubException("Rate limit exceeded.");

        var code = Passcode.Normalize(passcode);
        var name = Validation.ValidateName(displayName);

        if (!_reg.TryGet(code, out var room))
        {
            room = _reg.CreateRoom(code);
        }

        var isOwner = false;
        if (room.OwnerConnectionId is null)
        {
            room.OwnerConnectionId = connectionId;
            isOwner = true;
        }

        await Groups.AddToGroupAsync(connectionId, code);

        room.Participants[connectionId] = new Participant
        {
            ConnectionId = connectionId,
            DisplayName = name,
            LastSeenUtc = DateTimeOffset.UtcNow
        };
        room.LastActivityUtc = DateTimeOffset.UtcNow;

        var names = room.Participants.Values.Select(p => p.DisplayName).OrderBy(s => s).ToArray();
        await Clients.Group(code).SendAsync("UserJoined", new { displayName = name, participants = names });
        await Clients.Group(code).SendAsync("PresenceList", new { participants = names });

        Log.Information("User joined (count={Count}).", names.Length);
        return new JoinResult { Success = true, IsOwner = isOwner, NormalizedDisplayName = name };
    }

    public async Task SendMessage(string passcode, string text)
    {
        var connectionId = Context.ConnectionId;
        if (string.IsNullOrEmpty(connectionId))
            throw new HubException("Invalid connection state.");

        var code = Passcode.Normalize(passcode);
        var msg = Validation.ValidateMessage(text);
        if (!_reg.TryGet(code, out var room)) throw new HubException("Room missing.");

        if (!room.Participants.TryGetValue(connectionId, out var p))
            throw new HubException("Not a participant.");

        var safe = Validation.HtmlEncode(msg);
        var utc = DateTimeOffset.UtcNow.UtcDateTime.ToString("o");

        await Clients.Group(code).SendAsync("ReceiveMessage", new { displayName = p.DisplayName, text = safe, utc });

        p.LastSeenUtc = DateTimeOffset.UtcNow;
        room.LastActivityUtc = p.LastSeenUtc;
    }

    public Task Heartbeat(string passcode)
    {
        var connectionId = Context.ConnectionId;
        if (string.IsNullOrEmpty(connectionId)) return Task.CompletedTask;

        var code = Passcode.Normalize(passcode);
        if (!_reg.TryGet(code, out var room)) return Task.CompletedTask;

        if (room.Participants.TryGetValue(connectionId, out var p))
        {
            p.LastSeenUtc = DateTimeOffset.UtcNow;
            room.LastActivityUtc = p.LastSeenUtc;
        }
        return Task.CompletedTask;
    }

    public async Task EndRoom(string passcode)
    {
        var connectionId = Context.ConnectionId;
        if (string.IsNullOrEmpty(connectionId))
            throw new HubException("Invalid connection state.");

        var code = Passcode.Normalize(passcode);
        if (!_reg.TryGet(code, out var room)) return;

        if (room.OwnerConnectionId != connectionId)
            throw new HubException("Only host can end room.");

        await Clients.Group(code).SendAsync("RoomClosed");
        _reg.RemoveRoom(code);
        Log.Information("Room ended by host.");
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var connectionId = Context.ConnectionId;
        if (!string.IsNullOrEmpty(connectionId))
        {
            foreach (var room in _reg.AllRooms())
            {
                if (room.Participants.TryRemove(connectionId, out var removed))
                {
                    var names = room.Participants.Values.Select(v => v.DisplayName).OrderBy(s => s).ToArray();
                    await Clients.Group(room.Passcode)
                        .SendAsync("UserLeft", new { displayName = removed.DisplayName, participants = names });
                    await Clients.Group(room.Passcode).SendAsync("PresenceList", new { participants = names });
                    room.LastActivityUtc = DateTimeOffset.UtcNow;
                    Log.Information("User left room.");
                }
            }
        }
        await base.OnDisconnectedAsync(ex);
    }
}
