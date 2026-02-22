using System.Text.Json;

namespace Companion.Core;

public sealed class CompanionConfig
{
    public int TcpPort { get; set; } = 9876;
    public int UdpPort { get; set; } = 9877;
    public int DiscoveryPort { get; set; } = 9878;

    public string ServerName { get; set; } = "Outward Dynasty Companion";
    public bool EnableLanDiscovery { get; set; } = true;

    public static string ConfigPath => Path.Combine(AppPaths.BaseDir, "config.json");

    public static CompanionConfig LoadOrCreate()
    {
        Directory.CreateDirectory(AppPaths.BaseDir);
        if (!File.Exists(ConfigPath))
        {
            var cfg = new CompanionConfig();
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            return cfg;
        }
        return JsonSerializer.Deserialize<CompanionConfig>(File.ReadAllText(ConfigPath)) ?? new CompanionConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.BaseDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
