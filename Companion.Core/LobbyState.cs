using System.Text.Json;
using Companion.Protocol;

namespace Companion.Core;

public sealed class LobbyState
{
    public string LobbyCode { get; private set; }
    public string? HostPlayerId { get; private set; }
    public bool Paused { get; private set; }

    // single active lobby only
    public Dictionary<string, ConnectedClient> Clients { get; } = new();

    public string DynastyId { get; private set; } = "default";

    public LobbyState(string lobbyCode)
    {
        LobbyCode = lobbyCode;
    }

    public void SetDynasty(string dynastyId) => DynastyId = dynastyId;

    public void SetPaused(bool paused) => Paused = paused;

    public void SetHost(string hostId) => HostPlayerId = hostId;

    public SessionStatePayload ToSessionStatePayload()
    {
        var payload = new SessionStatePayload
        {
            Host = HostPlayerId,
            Paused = Paused,
            Players = Clients.Values.Select(c => new PlayerInfo
            {
                Id = c.PlayerId,
                Name = c.Name,
                Role = c.Role,
                ConnectedAt = c.ConnectedAt.ToUnixTimeSeconds(),
                LastSeenAt = c.LastSeenAt.ToUnixTimeSeconds(),
            }).ToList()
        };
        return payload;
    }
}
