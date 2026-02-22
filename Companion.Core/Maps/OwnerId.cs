namespace Companion.Core.Maps;

public static class OwnerId
{
    public static string Make(string type, string id) => $"{type}:{id}";
}
