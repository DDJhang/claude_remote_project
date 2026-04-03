using System.Net.WebSockets;

namespace WsChatServer.Models;

public class ChatClient
{
    public Guid Id { get; } = Guid.NewGuid();
    public WebSocket Socket { get; }
    public string? Nickname { get; set; }
    public string? RoomId { get; set; }

    public ChatClient(WebSocket socket)
    {
        Socket = socket;
    }

    public bool IsConnected =>
        Socket.State == WebSocketState.Open;
}
