using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ffmt.Core.Worlds;

public sealed record VersionedItemNames(IReadOnlyDictionary<int, string> Names, string Version)
{
    /// <summary>The version is a content hash, so it only changes when an id or name does. The SPA
    /// caches the name list forever under it.</summary>
    public static VersionedItemNames From(IReadOnlyDictionary<int, string> names)
    {
        var content = new StringBuilder();
        foreach (var (id, name) in names.OrderBy(kv => kv.Key))
        {
            content.Append(CultureInfo.InvariantCulture, $"{id}\t{name}\n");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString()));
        return new VersionedItemNames(names, Convert.ToHexStringLower(hash, 0, 8));
    }
}
