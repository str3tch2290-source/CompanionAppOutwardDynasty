using System.Text.Json;
using System.Text.Json.Serialization;

namespace Companion.Protocol;

public sealed class Envelope
{
    [JsonPropertyName("v")] public int Version { get; set; } = ProtocolVersion.Current;
    [JsonPropertyName("op")] public string Op { get; set; } = "";
    [JsonPropertyName("ts")] public long UnixTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [JsonPropertyName("lobby")] public string? Lobby { get; set; }
    [JsonPropertyName("sender")] public string? Sender { get; set; }
    [JsonPropertyName("payload")] public JsonElement Payload { get; set; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Envelope FromJson(string json) =>
        JsonSerializer.Deserialize<Envelope>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to parse envelope.");

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public T? PayloadAs<T>() => Payload.ValueKind == JsonValueKind.Undefined ? default : Payload.Deserialize<T>(JsonOptions);
}

public sealed class HelloPayload
{
    [JsonPropertyName("token")] public string Token { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "client"; // host|client
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("build")] public string? Build { get; set; }
}

public sealed class HelloAckPayload
{
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = "";
    [JsonPropertyName("udpPort")] public int UdpPort { get; set; }
    [JsonPropertyName("serverTime")] public long ServerTime { get; set; }
}

public sealed class WarnVersionPayload
{
    [JsonPropertyName("serverVersion")] public int ServerVersion { get; set; }
    [JsonPropertyName("clientVersion")] public int ClientVersion { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

public sealed class PausePayload
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "hard";
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
}

public sealed class SessionStatePayload
{
    [JsonPropertyName("host")] public string? Host { get; set; }
    [JsonPropertyName("players")] public List<PlayerInfo> Players { get; set; } = new();
    [JsonPropertyName("paused")] public bool Paused { get; set; }
}

public sealed class PlayerInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "client"; // host|client
    [JsonPropertyName("connectedAt")] public long ConnectedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [JsonPropertyName("lastSeenAt")] public long LastSeenAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public sealed class VoteBeginPayload
{
    [JsonPropertyName("reason")] public string Reason { get; set; } = "host_down";
    [JsonPropertyName("candidates")] public List<string> Candidates { get; set; } = new();
    [JsonPropertyName("deadlineSec")] public int DeadlineSec { get; set; } = 30;
}

public sealed class VoteCastPayload
{
    [JsonPropertyName("candidate")] public string Candidate { get; set; } = "";
}

public sealed class VoteResultPayload
{
    [JsonPropertyName("winner")] public string Winner { get; set; } = "";
    [JsonPropertyName("tally")] public Dictionary<string,int> Tally { get; set; } = new();
}

public sealed class HostAssignPayload
{
    [JsonPropertyName("host")] public string Host { get; set; } = "";
}

public sealed class SnapshotPushPayload
{
    [JsonPropertyName("dynastyId")] public string DynastyId { get; set; } = "";
    [JsonPropertyName("time")] public Dictionary<string,int> Time { get; set; } = new();
    [JsonPropertyName("snapshot")] public JsonElement Snapshot { get; set; }
}

public sealed class SnapshotAckPayload
{
    [JsonPropertyName("hash")] public string Hash { get; set; } = "";
    [JsonPropertyName("stored")] public bool Stored { get; set; } = true;
}

public sealed class SnapshotTransferPayload
{
    [JsonPropertyName("dynastyId")] public string DynastyId { get; set; } = "";
    [JsonPropertyName("hash")] public string Hash { get; set; } = "";
    [JsonPropertyName("time")] public Dictionary<string,int> Time { get; set; } = new();
    [JsonPropertyName("snapshot")] public JsonElement Snapshot { get; set; }
}

public sealed class HistoryEventPayload
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("dynastyId")] public string DynastyId { get; set; } = "";
    [JsonPropertyName("data")] public JsonElement Data { get; set; }
}

public sealed class TerritoryDeltaPayload
{
    [JsonPropertyName("region")] public string Region { get; set; } = "";
    [JsonPropertyName("cells")] public List<TerritoryCell> Cells { get; set; } = new();
}

public sealed class TerritoryCell
{
    [JsonPropertyName("q")] public int Q { get; set; }
    [JsonPropertyName("r")] public int R { get; set; }
    [JsonPropertyName("owner")] public TerritoryOwner Owner { get; set; } = new();
}

public sealed class TerritoryOwner
{
    [JsonPropertyName("type")] public string Type { get; set; } = "faction"; // faction|dynasty|npc
    [JsonPropertyName("id")] public string Id { get; set; } = "";
}
