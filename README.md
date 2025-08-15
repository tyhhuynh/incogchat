# IncogChat Server (work in progress...)

## overview

create/join chat rooms that run entirely in-memory (no database), kicks idle users, and automatically expires empty rooms, leaving no trace behind

## features

- real-time messaging (SignalR)
- anonymous chat rooms
- host controls
- auto-cleanup

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

## Getting Started

Clone the repo and run the server:

```bash
git clone https://github.com/yourusername/incogchat-server.git
cd incogchat-server

# restore dependencies
dotnet restore

# run server
dotnet run --project src/IncogChat.Server
```

## Configuration

### Defaults:

- `Port: 8080 (http)`
- `CORS allow-list: PORTFOLIO_ORIGIN=http://localhost:3000`

### Environment Variables (optional):

```bash
PORT=8080                                    # Server port
PORTFOLIO_ORIGIN=https://your-domain.com    # CORS allowed origin
USER_IDLE_MINUTES=5                         # Minutes before kicking idle users
ROOM_TTL_MINUTES=10                         # Minutes before expiring empty rooms
```

## API Reference

### REST Endpoints

- `POST /rooms` → `200 { "passcode": "########" }`
- `GET /health` → `200 { "ok": true }`

### SignalR Hub

- **Endpoint**: `/hubs/chat`

### Hub Methods (client→server)

- `CreateRoom(): string` - Creates a new room and returns passcode
- `JoinRoom(passcode: string, displayName: string): { success, isOwner, normalizedDisplayName }` - Joins existing room
- `SendMessage(passcode: string, text: string): void` - Sends message to room
- `Heartbeat(passcode: string): void` - Keeps user active in room
- `EndRoom(passcode: string): void` - Ends room (owner only)

### Events (server→client)

- `ReceiveMessage({ displayName, text, utc })` - New message received
- `UserJoined({ displayName, participants })` - User joined room
- `UserLeft({ displayName, participants })` - User left room
- `PresenceList({ participants })` - Updated participant list
- `RoomClosed()` - Room was ended by owner
- `KickedForInactivity()` - User kicked for being idle

## Architecture

### Key Components

- **ChatHub**: SignalR hub handling real-time communication
- **RoomRegistry**: In-memory room management and state
- **RoomSweeper**: Background service for cleaning up expired rooms
- **UserInactivitySweeper**: Background service for removing idle users
- **ActionRateLimiter**: Rate limiting for API endpoints

### Data Flow

1. Client connects to SignalR hub
2. Room creation/joining via hub methods
3. Real-time messaging through hub events
4. Automatic cleanup via background services

### Project Structure

```
src/IncogChat.Server/
├── Core/           # Core models and business logic
├── Hubs/           # SignalR hub implementations
├── Services/       # Background services
├── Infra/          # Infrastructure components
└── Program.cs      # Application entry point
```

## Troubleshooting

### Common Issues

**Port already in use:**

```bash
# Check what's using port 8080
lsof -i :8080
# Kill process or change PORT environment variable
```

**CORS errors:**

- Ensure `PORTFOLIO_ORIGIN` is set correctly
- Check that your frontend origin matches the allowed origin

**Connection issues:**

- Verify SignalR hub endpoint: `/hubs/chat`
- Check browser console for connection errors
- Ensure server is running on expected port

### Logs

Enable detailed logging by setting:

```bash
ASPNETCORE_ENVIRONMENT=Development
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
