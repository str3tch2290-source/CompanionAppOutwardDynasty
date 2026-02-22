using System.Runtime.InteropServices;

namespace Companion.Core;

public static class AppPaths
{
    public static string BaseDir
    {
        get
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(dir, "OutwardDynastyCompanion");
        }
    }

    public static string EnsureDynastyDir(string dynastyId)
    {
        var d = Path.Combine(BaseDir, "dynasties", Sanitize(dynastyId));
        Directory.CreateDirectory(d);
        Directory.CreateDirectory(Path.Combine(d, "snapshots"));
        Directory.CreateDirectory(Path.Combine(d, "history"));
        Directory.CreateDirectory(Path.Combine(d, "maps"));
        Directory.CreateDirectory(Path.Combine(d, "maps", "overlays"));
        return d;
    }

    public static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
