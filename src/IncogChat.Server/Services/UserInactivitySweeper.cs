namespace IncogChat.Server.Services;

using IncogChat.Server.Core;
using IncogChat.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog;

public sealed class UserInactivitySweeper : BackgroundService
{
    private readonly RoomRegistry _reg;
    private readonly IHubContext<ChatHub> _hub;

    public UserInactivitySweeper(RoomRegistry reg, IHubContext<ChatHub> hub)
    {
        _reg = reg; _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var room in _reg.AllRooms())
            {
                foreach (var kv in room.Participants.ToArray())
                {
                    var p = kv.Value;
                    if (now - p.LastSeenUtc > ServerConfig.UserIdle)
                    {
                        var name = p.DisplayName;
                        await _hub.Clients.Client(p.ConnectionId).SendAsync("KickedForInactivity");
                        await _hub.Groups.RemoveFromGroupAsync(p.ConnectionId, room.Passcode);
                        room.Participants.TryRemove(p.ConnectionId, out _);

                        var names = room.Participants.Values.Select(v => v.DisplayName).OrderBy(s => s).ToArray();
                        await _hub.Clients.Group(room.Passcode)
                            .SendAsync("UserLeft", new { displayName = name, participants = names });
                        await _hub.Clients.Group(room.Passcode).SendAsync("PresenceList", new { participants = names });

                        Log.Information("Kicked inactive user.");
                    }
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
