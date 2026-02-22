using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Companion.Core;

public sealed class SnapshotStore
{
    private readonly string _dynastyDir;

    public SnapshotStore(string dynastyId)
    {
        _dynastyDir = AppPaths.EnsureDynastyDir(dynastyId);
    }

    public (string hash, string path) SaveSnapshot(JsonElement snapshot, Dictionary<string,int> time)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timePart = time.TryGetValue("day", out var d) && time.TryGetValue("hour", out var h) ? $"d{d:0000}_h{h:00}" : "time_unknown";
        var json = snapshot.GetRawText();

        var hash = ComputeSha256(json);
        var file = Path.Combine(_dynastyDir, "snapshots", $"snapshot_{timePart}_{ts}_{hash[..8]}.json");
        File.WriteAllText(file, json);

        var indexPath = Path.Combine(_dynastyDir, "snapshots", "index.json");
        var index = new List<Dictionary<string, object?>>();
        if (File.Exists(indexPath))
            index = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(File.ReadAllText(indexPath)) ?? new();

        index.Add(new Dictionary<string, object?>
        {
            ["ts"] = ts,
            ["hash"] = hash,
            ["file"] = Path.GetFileName(file),
            ["time"] = time
        });

        File.WriteAllText(indexPath, JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }));
        return (hash, file);
    }

    public (string hash, JsonDocument doc)? LoadLatest()
    {
        var indexPath = Path.Combine(_dynastyDir, "snapshots", "index.json");
        if (!File.Exists(indexPath)) return null;

        var index = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(File.ReadAllText(indexPath)) ?? new();
        if (index.Count == 0) return null;

        // latest by ts
        var latest = index.OrderByDescending(e => Convert.ToInt64(e["ts"]!)).First();
        var hash = (string)latest["hash"]!;
        var file = (string)latest["file"]!;
        var full = Path.Combine(_dynastyDir, "snapshots", file);
        if (!File.Exists(full)) return null;

        var doc = JsonDocument.Parse(File.ReadAllText(full));
        return (hash, doc);
    }

    private static string ComputeSha256(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
