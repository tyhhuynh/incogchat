namespace IncogChat.Server.Services;

using IncogChat.Server.Core;
using IncogChat.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog;

public sealed class RoomSweeper : BackgroundService
{
    private readonly RoomRegistry _reg;
    private readonly IHubContext<ChatHub> _hub;

    public RoomSweeper(RoomRegistry reg, IHubContext<ChatHub> hub)
    {
        _reg = reg; _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var room in _reg.AllRooms().ToArray())
            {
                if (now - room.LastActivityUtc > ServerConfig.RoomTtl)
                {
                    await _hub.Clients.Group(room.Passcode).SendAsync("RoomClosed");
                    _reg.RemoveRoom(room.Passcode);
                    Log.Information("Room TTL expired; closed.");
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
