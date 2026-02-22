namespace Companion.Core;

public sealed class ConnectedClient
{
    public string PlayerId { get; init; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "client";
    public DateTimeOffset ConnectedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
}
