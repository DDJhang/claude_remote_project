using System.Collections.Concurrent;

namespace WsChatServer.Models;

public class ChatRoom
{
    public string Id { get; }
    private readonly ConcurrentDictionary<Guid, ChatClient> _clients = new();

    public ChatRoom(string id)
    {
        Id = id;
    }

    public bool AddClient(ChatClient client) =>
        _clients.TryAdd(client.Id, client);

    public bool RemoveClient(Guid clientId) =>
        _clients.TryRemove(clientId, out _);

    public IEnumerable<ChatClient> GetClients() =>
        _clients.Values;

    public bool IsEmpty => _clients.IsEmpty;
}
