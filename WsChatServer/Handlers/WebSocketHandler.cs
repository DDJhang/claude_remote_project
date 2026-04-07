using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WsChatServer.Models;
using WsChatServer.Services;

namespace WsChatServer.Handlers;

public class WebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly RoomManager _roomManager;
    private readonly ILogger<WebSocketHandler> _logger;

    public WebSocketHandler(RoomManager roomManager, ILogger<WebSocketHandler> logger)
    {
        _roomManager = roomManager;
        _logger = logger;
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken ct)
    {
        var client = new ChatClient(socket);
        _logger.LogInformation("Client {Id} connected.", client.Id);

        try
        {
            await ReceiveLoopAsync(client, ct);
        }
        finally
        {
            await DisconnectClientAsync(client);
            _logger.LogInformation("Client {Id} disconnected.", client.Id);
        }
    }

    // ── Receive loop ─────────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(ChatClient client, CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (client.IsConnected && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            using var ms = new System.IO.MemoryStream();

            try
            {
                do
                {
                    result = await client.Socket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning("Client {Id} disconnected unexpectedly: {Msg}", client.Id, ex.Message);
                return;
            }

            var json = Encoding.UTF8.GetString(ms.ToArray());
            await ProcessMessageAsync(client, json);
        }
    }

    // ── Message dispatch ─────────────────────────────────────────────────────

    private async Task ProcessMessageAsync(ChatClient client, string json)
    {
        ClientMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<ClientMessage>(json, JsonOpts);
        }
        catch
        {
            await SendAsync(client, new ErrorMessage { Message = "Invalid JSON." });
            return;
        }

        if (msg is null)
        {
            await SendAsync(client, new ErrorMessage { Message = "Empty message." });
            return;
        }

        switch (msg.Action.ToLower())
        {
            case "create":
                await HandleCreateAsync(client, msg);
                break;
            case "join":
                await HandleJoinAsync(client, msg);
                break;
            case "message":
                await HandleChatAsync(client, msg);
                break;
            default:
                await SendAsync(client, new ErrorMessage { Message = $"Unknown action: {msg.Action}" });
                break;
        }
    }

    // ── Action handlers ──────────────────────────────────────────────────────

    private async Task HandleCreateAsync(ChatClient client, ClientMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Nickname))
        {
            await SendAsync(client, new ErrorMessage { Message = "Nickname is required." });
            return;
        }

        if (client.RoomId is not null)
        {
            await SendAsync(client, new ErrorMessage { Message = "You are already in a room." });
            return;
        }

        var room = _roomManager.CreateRoom();
        client.Nickname = msg.Nickname.Trim();
        client.RoomId = room.Id;
        room.AddClient(client);

        _logger.LogInformation("Client {Id} ({Nick}) created room {Room}.", client.Id, client.Nickname, room.Id);

        await SendAsync(client, new RoomCreatedMessage { RoomId = room.Id });
    }

    private async Task HandleJoinAsync(ChatClient client, ClientMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.RoomId) || string.IsNullOrWhiteSpace(msg.Nickname))
        {
            await SendAsync(client, new ErrorMessage { Message = "RoomId and Nickname are required." });
            return;
        }

        if (client.RoomId is not null)
        {
            await SendAsync(client, new ErrorMessage { Message = "You are already in a room." });
            return;
        }

        var room = _roomManager.GetRoom(msg.RoomId.Trim());
        if (room is null)
        {
            await SendAsync(client, new ErrorMessage { Message = $"Room '{msg.RoomId}' not found." });
            return;
        }

        client.Nickname = msg.Nickname.Trim();
        client.RoomId = room.Id;
        room.AddClient(client);

        _logger.LogInformation("Client {Id} ({Nick}) joined room {Room}.", client.Id, client.Nickname, room.Id);

        // Notify the joining client
        await SendAsync(client, new RoomJoinedMessage { RoomId = room.Id, Nickname = client.Nickname });

        // Notify everyone else in the room
        await BroadcastAsync(room, new UserEventMessage
        {
            Type = "userJoined",
            Nickname = client.Nickname
        }, excludeClientId: client.Id);
    }

    private async Task HandleChatAsync(ChatClient client, ClientMessage msg)
    {
        if (client.RoomId is null)
        {
            await SendAsync(client, new ErrorMessage { Message = "You must join a room first." });
            return;
        }

        if (string.IsNullOrWhiteSpace(msg.Content))
        {
            await SendAsync(client, new ErrorMessage { Message = "Message content cannot be empty." });
            return;
        }

        var room = _roomManager.GetRoom(client.RoomId);
        if (room is null) return;

        var chatMsg = new ChatMessage
        {
            MessageType = "normal",
            From = client.Nickname ?? "Unknown",
            Content = msg.Content,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };

        await BroadcastAsync(room, chatMsg);
    }

    // ── Disconnect cleanup ───────────────────────────────────────────────────

    private async Task DisconnectClientAsync(ChatClient client)
    {
        if (client.RoomId is null) return;

        var room = _roomManager.GetRoom(client.RoomId);
        if (room is null) return;

        room.RemoveClient(client.Id);

        if (!room.IsEmpty && client.Nickname is not null)
        {
            await BroadcastAsync(room, new UserEventMessage
            {
                Type = "userLeft",
                Nickname = client.Nickname
            });
        }

        _roomManager.RemoveRoomIfEmpty(client.RoomId);
    }

    // ── Send helpers ─────────────────────────────────────────────────────────

    private async Task SendAsync(ChatClient client, object message)
    {
        if (!client.IsConnected) return;

        var json = JsonSerializer.Serialize(message, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await client.Socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send to client {Id}: {Ex}", client.Id, ex.Message);
        }
    }

    private async Task BroadcastAsync(ChatRoom room, object message, Guid? excludeClientId = null)
    {
        var tasks = room.GetClients()
            .Where(c => c.IsConnected && c.Id != excludeClientId)
            .Select(c => SendAsync(c, message));

        await Task.WhenAll(tasks);
    }

    // ── 系統廣播（供 HTTP 端點呼叫）────────────────────────────────────────────

    public async Task BroadcastSystemMessageAsync(string content)
    {
        var msg = new ChatMessage
        {
            MessageType = "system",
            From = "System",
            Content = content,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };

        var tasks = _roomManager.GetAllClients()
            .Where(c => c.IsConnected)
            .Select(c => SendAsync(c, msg));

        await Task.WhenAll(tasks);
        _logger.LogInformation("System broadcast sent: {Content}", content);
    }
}
