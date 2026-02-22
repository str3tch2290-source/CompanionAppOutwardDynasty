using System.Text.Json;

namespace Companion.Core.Maps;

public sealed class TerritoryGridStore
{
    public const int DefaultW = 128;
    public const int DefaultH = 128;

    private readonly string _dynastyId;
    private readonly string _dynastyDir;

    // region -> grid
    private readonly Dictionary<string, TerritoryGrid> _grids = new(StringComparer.OrdinalIgnoreCase);

    public TerritoryGridStore(string dynastyId)
    {
        _dynastyId = dynastyId;
        _dynastyDir = AppPaths.EnsureDynastyDir(dynastyId);
    }

    public TerritoryGrid GetOrCreate(string region, int w = DefaultW, int h = DefaultH)
    {
        if (_grids.TryGetValue(region, out var g)) return g;
        g = Load(region) ?? new TerritoryGrid(region, w, h);
        _grids[region] = g;
        return g;
    }

    public void ApplyCells(string region, IEnumerable<(int x, int y, string ownerKey)> cells)
    {
        var g = GetOrCreate(region);
        foreach (var (x, y, owner) in cells)
        {
            if (x < 0 || y < 0 || x >= g.W || y >= g.H) continue;
            g.Set(x, y, owner);
        }
        Save(g);
    }

    public void Save(TerritoryGrid g)
    {
        var dir = Path.Combine(_dynastyDir, "maps", "overlays", AppPaths.Sanitize(g.Region));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "grid.json");

        var dto = new GridDto
        {
            Region = g.Region,
            W = g.W,
            H = g.H,
            Rle = g.ToRle().ToList()
        };

        File.WriteAllText(path, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }));
    }

    public TerritoryGrid? Load(string region)
    {
        var dir = Path.Combine(_dynastyDir, "maps", "overlays", AppPaths.Sanitize(region));
        var path = Path.Combine(dir, "grid.json");
        if (!File.Exists(path)) return null;

        var dto = JsonSerializer.Deserialize<GridDto>(File.ReadAllText(path));
        if (dto is null) return null;
        return TerritoryGrid.FromRle(dto.Region ?? region, dto.W, dto.H, dto.Rle ?? new());
    }

    private sealed class GridDto
    {
        public string? Region { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public List<RleRun>? Rle { get; set; }
    }
}

public sealed class TerritoryGrid
{
    public string Region { get; }
    public int W { get; }
    public int H { get; }

    // Owner string keys, length = W*H
    private readonly string[] _cells;

    public TerritoryGrid(string region, int w, int h)
    {
        Region = region;
        W = w;
        H = h;
        _cells = new string[W * H];
        Array.Fill(_cells, "npc:Unknown");
    }

    public string Get(int x, int y) => _cells[y * W + x];

    public void Set(int x, int y, string ownerKey) => _cells[y * W + x] = ownerKey;

    public IEnumerable<RleRun> ToRle()
    {
        var runs = new List<RleRun>();
        if (_cells.Length == 0) return runs;

        var cur = _cells[0];
        var count = 1;
        for (int i = 1; i < _cells.Length; i++)
        {
            var v = _cells[i];
            if (string.Equals(v, cur, StringComparison.Ordinal))
            {
                count++;
                continue;
            }
            runs.Add(new RleRun { Owner = cur, Count = count });
            cur = v;
            count = 1;
        }
        runs.Add(new RleRun { Owner = cur, Count = count });
        return runs;
    }

    public static TerritoryGrid FromRle(string region, int w, int h, List<RleRun> runs)
    {
        var g = new TerritoryGrid(region, w, h);
        var idx = 0;
        foreach (var r in runs)
        {
            for (int i = 0; i < r.Count; i++)
            {
                if (idx >= g._cells.Length) break;
                g._cells[idx++] = r.Owner ?? "npc:Unknown";
            }
        }
        // fill remainder
        while (idx < g._cells.Length) g._cells[idx++] = "npc:Unknown";
        return g;
    }
}

public sealed class RleRun
{
    public string? Owner { get; set; }
    public int Count { get; set; }
}
