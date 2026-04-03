namespace WsChatServer.Models;

// ── HTTP Request ──────────────────────────────────────────────────────────────

public class RoomRequest
{
    public string RoomId { get; set; } = string.Empty;
}

// ── Client → Server ──────────────────────────────────────────────────────────

public class ClientMessage
{
    public string Action { get; set; } = string.Empty; // "create" | "join" | "message"
    public string? Nickname { get; set; }
    public string? RoomId { get; set; }
    public string? Content { get; set; }
}

// ── Server → Client ──────────────────────────────────────────────────────────

public abstract class ServerMessage
{
    public string Type { get; set; } = string.Empty;
}

public class RoomCreatedMessage : ServerMessage
{
    public string RoomId { get; set; } = string.Empty;
    public RoomCreatedMessage() => Type = "created";
}

public class RoomJoinedMessage : ServerMessage
{
    public string RoomId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public RoomJoinedMessage() => Type = "joined";
}

public class ChatMessage : ServerMessage
{
    public string From { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public ChatMessage() => Type = "message";
}

public class UserEventMessage : ServerMessage
{
    public string Nickname { get; set; } = string.Empty;
    // Type = "userJoined" | "userLeft"
}

public class ErrorMessage : ServerMessage
{
    public string Message { get; set; } = string.Empty;
    public ErrorMessage() => Type = "error";
}
