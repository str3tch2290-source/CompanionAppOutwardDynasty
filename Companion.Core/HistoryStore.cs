using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Companion.Core;

public sealed class HistoryStore : IDisposable
{
    private readonly string _dynastyDir;
    private readonly string _jsonlPath;
    private readonly string _dbPath;
    private SqliteConnection? _conn;

    public HistoryStore(string dynastyId)
    {
        _dynastyDir = AppPaths.EnsureDynastyDir(dynastyId);
        _jsonlPath = Path.Combine(_dynastyDir, "history", "history.jsonl");
        _dbPath = Path.Combine(_dynastyDir, "history", "history.db");
        EnsureDb();
    }

    private void EnsureDb()
    {
        _conn = new SqliteConnection($"Data Source={_dbPath}");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ts INTEGER NOT NULL,
  type TEXT NOT NULL,
  sender TEXT,
  json TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_events_ts ON events(ts);
CREATE INDEX IF NOT EXISTS idx_events_type ON events(type);
";
        cmd.ExecuteNonQuery();
    }

    public void Append(long ts, string type, string? sender, JsonElement json)
    {
        // JSONL raw
        var lineObj = new Dictionary<string, object?>
        {
            ["ts"] = ts,
            ["type"] = type,
            ["sender"] = sender,
            ["data"] = json
        };
        var line = JsonSerializer.Serialize(lineObj);
        Directory.CreateDirectory(Path.GetDirectoryName(_jsonlPath)!);
        File.AppendAllText(_jsonlPath, line + Environment.NewLine);

        // SQLite index
        if (_conn is null) return;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO events(ts,type,sender,json) VALUES ($ts,$type,$sender,$json)";
        cmd.Parameters.AddWithValue("$ts", ts);
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$sender", (object?)sender ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$json", json.GetRawText());
        cmd.ExecuteNonQuery();
    }

    public List<(long ts, string type, string? sender, string json)> Query(int limit = 200, string? type = null)
    {
        if (_conn is null) return new();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = type is null
            ? "SELECT ts,type,sender,json FROM events ORDER BY ts DESC LIMIT $limit"
            : "SELECT ts,type,sender,json FROM events WHERE type=$type ORDER BY ts DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        if (type is not null) cmd.Parameters.AddWithValue("$type", type);

        var res = new List<(long, string, string?, string)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            res.Add((r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3)));
        }
        return res;
    }

    public void Dispose()
    {
        _conn?.Dispose();
        _conn = null;
    }
}
