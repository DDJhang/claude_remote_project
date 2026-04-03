using System.Collections.Concurrent;
using WsChatServer.Models;

namespace WsChatServer.Services;

public class RoomManager
{
    private const string Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int IdLength = 4;
    private const int MaxRetries = 200;

    private readonly ConcurrentDictionary<string, ChatRoom> _rooms = new();
    private readonly Random _rng = Random.Shared;

    public ChatRoom CreateRoom()
    {
        for (int i = 0; i < MaxRetries; i++)
        {
            var id = GenerateId();
            var room = new ChatRoom(id);
            if (_rooms.TryAdd(id, room))
                return room;
        }
        throw new InvalidOperationException("Unable to generate a unique room ID. Server is full.");
    }

    public ChatRoom? GetRoom(string roomId) =>
        _rooms.TryGetValue(roomId, out var room) ? room : null;

    public int RoomCount => _rooms.Count;

    public int TotalConnectionCount =>
        _rooms.Values.Sum(r => r.GetClients().Count());

    public IEnumerable<string>? GetNicknamesInRoom(string roomId) =>
        _rooms.TryGetValue(roomId, out var room)
            ? room.GetClients().Select(c => c.Nickname ?? "(未命名)")
            : null;

    public IEnumerable<ChatClient> GetAllClients() =>
        _rooms.Values.SelectMany(r => r.GetClients());

    public void RemoveRoomIfEmpty(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room) && room.IsEmpty)
            _rooms.TryRemove(roomId, out _);
    }

    private string GenerateId()
    {
        return new string(Enumerable.Range(0, IdLength)
            .Select(_ => Chars[_rng.Next(Chars.Length)])
            .ToArray());
    }
}
