using WsChatServer.Handlers;
using WsChatServer.Models;
using WsChatServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

app.UseStaticFiles();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket connection required.");
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
    await handler.HandleAsync(socket, context.RequestAborted);
});

app.MapGet("/", () => "WsChatServer is running. Connect via WebSocket at /ws");

// ── HTTP API ──────────────────────────────────────────────────────────────────

// 1. 取得現在聊天室數量
app.MapGet("/api/rooms/count", (RoomManager rm) =>
    Results.Ok(new { count = rm.RoomCount }));

// 2. 取得現在連線數量
app.MapGet("/api/connections/count", (RoomManager rm) =>
    Results.Ok(new { count = rm.TotalConnectionCount }));

// 3. 取得指定聊天室的所有客戶暱稱
app.MapPost("/api/rooms/clients", (RoomRequest req, RoomManager rm) =>
{
    var nicknames = rm.GetNicknamesInRoom(req.RoomId);
    return nicknames is null
        ? Results.NotFound(new { message = $"Room '{req.RoomId}' not found." })
        : Results.Ok(new { roomId = req.RoomId, nicknames });
});

app.Run();
