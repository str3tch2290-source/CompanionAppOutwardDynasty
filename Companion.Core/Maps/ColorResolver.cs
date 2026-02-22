using System.Drawing;

namespace Companion.Core.Maps;

public static class ColorResolver
{
    // Hardcoded defaults per user instruction.
    // Unknowns fall back to a bright magenta-like color.
    private static readonly Dictionary<string, Color> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Major factions
        ["faction:BlueChamber"] = ColorTranslator.FromHtml("#2E5BFF"),
        ["faction:Levant"] = ColorTranslator.FromHtml("#D4A017"),
        ["faction:HolyMission"] = ColorTranslator.FromHtml("#C8D6E5"),
        ["faction:Soroboreans"] = ColorTranslator.FromHtml("#008B8B"),
        ["faction:CalderaAuthority"] = ColorTranslator.FromHtml("#B22222"),

        // NPC groups
        ["npc:Bandits"] = ColorTranslator.FromHtml("#8B0000"),
        ["npc:Trogs"] = ColorTranslator.FromHtml("#6A0DAD"),
        ["npc:Immaculates"] = ColorTranslator.FromHtml("#00FFFF"),
        ["npc:Undead"] = ColorTranslator.FromHtml("#556B2F"),
        ["npc:Unknown"] = ColorTranslator.FromHtml("#FF00AA"),
    };

    private static readonly Color[] DynastyPalette = new[]
    {
        ColorTranslator.FromHtml("#FF6B6B"),
        ColorTranslator.FromHtml("#4ECDC4"),
        ColorTranslator.FromHtml("#FFD93D"),
        ColorTranslator.FromHtml("#6C5CE7"),
        ColorTranslator.FromHtml("#00C853"),
        ColorTranslator.FromHtml("#FF8C00"),
        ColorTranslator.FromHtml("#9C27B0"),
        ColorTranslator.FromHtml("#00ACC1"),
    };

    private static readonly Dictionary<string, Color> DynastyAssigned = new(StringComparer.OrdinalIgnoreCase);

    public static Color Resolve(string ownerKey)
    {
        if (Known.TryGetValue(ownerKey, out var c))
            return c;

        if (ownerKey.StartsWith("dynasty:", StringComparison.OrdinalIgnoreCase))
        {
            if (!DynastyAssigned.TryGetValue(ownerKey, out var dc))
            {
                // Stable-ish assignment by hashing
                var idx = Math.Abs(ownerKey.GetHashCode()) % DynastyPalette.Length;
                dc = DynastyPalette[idx];
                DynastyAssigned[ownerKey] = dc;
            }
            return dc;
        }

        return ColorTranslator.FromHtml("#FF00AA");
    }

    public static Color Darken(Color c, float factor = 0.55f)
        => Color.FromArgb(c.A, (int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));

    public static Color Brighten(Color c, float factor = 1.15f)
        => Color.FromArgb(c.A,
            Math.Min(255, (int)(c.R * factor)),
            Math.Min(255, (int)(c.G * factor)),
            Math.Min(255, (int)(c.B * factor)));
}
