using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using IncogChat.Server.Core;
using IncogChat.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Serilog (no PII)
builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console();
});

// CORS allow-list (single origin)
var portfolioOrigin = Environment.GetEnvironmentVariable("PORTFOLIO_ORIGIN")
                    ?? builder.Configuration["PORTFOLIO_ORIGIN"]
                    ?? "https://example.com";

builder.Services.AddCors(options =>
{
    options.AddPolicy("portfolio", p =>
        p.WithOrigins(portfolioOrigin)
         .AllowAnyHeader()
         .AllowCredentials()
         .WithMethods("GET", "POST"));
});

// SignalR
builder.Services.AddSignalR();

// In-memory state
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddSingleton<IncogChat.Server.Infra.ActionRateLimiter>();

// Sweepers
builder.Services.AddHostedService<IncogChat.Server.Services.UserInactivitySweeper>();
builder.Services.AddHostedService<IncogChat.Server.Services.RoomSweeper>();

// HTTP endpoint rate-limits
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("create", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 3,
                TokensPerPeriod = 3,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("join-http", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors("portfolio");
app.UseRateLimiter();

// Health
app.MapGet("/health", () => Results.Ok(new { ok = true }))
   .RequireCors("portfolio");

// POST /rooms -> { passcode: "########" }
app.MapPost("/rooms", (RoomRegistry reg) =>
{
    var passcode = Passcode.GenerateUnique(reg);
    reg.CreateRoom(passcode);
    Log.Information("Room reserved via HTTP.");
    return Results.Ok(new { passcode });
})
.RequireCors("portfolio")
.RequireRateLimiting("create");

// SignalR hub
app.MapHub<ChatHub>("/hubs/chat").RequireCors("portfolio");

// Bind port
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();
