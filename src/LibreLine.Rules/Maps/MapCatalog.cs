// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Rules.Maps;

/// <summary>The maps the rules engine knows, by map enum.</summary>
public interface IMapCatalog
{
    IReadOnlyCollection<MapDefinition> All { get; }

    MapDefinition? Find(int mapEnum);
}

/// <summary>The map catalogue built from the map documents embedded in this assembly.</summary>
public sealed class MapCatalog : IMapCatalog
{
    private readonly SortedDictionary<int, MapDefinition> maps = [];

    public MapCatalog(IEnumerable<MapDefinition> maps)
    {
        foreach (var map in maps)
        {
            if (!this.maps.TryAdd(map.Enum, map))
            {
                throw new InvalidOperationException($"map enum {map.Enum} is defined twice");
            }
        }
    }

    public IReadOnlyCollection<MapDefinition> All => maps.Values;

    public MapDefinition? Find(int mapEnum) => maps.GetValueOrDefault(mapEnum);

    /// <summary>Parses every embedded map document.</summary>
    public static MapCatalog LoadEmbedded()
    {
        var assembly = typeof(MapCatalog).Assembly;
        var maps = new List<MapDefinition>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("maps/", StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            maps.AddRange(MapFileParser.Parse(name, reader.ReadToEnd()));
        }

        return new MapCatalog(maps);
    }
}
