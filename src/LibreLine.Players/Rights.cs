// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Players;

/// <summary>One entry of <c>Player.rights</c>: <c>{name: code, feature: code}</c>.</summary>
public sealed record AccessRight(string Name, string Feature);

/// <summary>Turns Asmodee <c>features</c> into the client's product codes (docs/protocol/asmodee.md).</summary>
public static class ProductCodes
{
    private const string BaseGame = "tickettorideUSA";

    private static readonly Dictionary<string, string> BasePlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Steam"] = "OLFT-TTOL-USAM-STEA",
        ["Standalone"] = "OLFT-TTOL-USAM-ALON",
        ["Google"] = "OLFT-TTOL-USAM-GOOG",
        ["Kindle"] = "OLFT-TTOL-USAM-KNDL",
        ["Pocket"] = "OLFT-TTOL-USAM-POCK",
        ["iPad"] = "OLFT-TTOL-USAM-IPAD",
    };

    private static readonly Dictionary<string, string> Maps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tickettoride1910"] = "OLFT-TTOL-1910",
        ["tickettorideEuro"] = "OLFT-TTOL-EURO",
        ["tickettorideSwiss"] = "OLFT-TTOL-SWIS",
        ["tickettorideAsia"] = "OLFT-TTOL-ASIA",
        ["tickettorideIndia"] = "OLFT-TTOL-INDI",
        ["tickettorideNordic"] = "OLFT-TTOL-NORD",
        ["tickettoridePennsylvania"] = "OLFT-TTOL-PENN",
        ["tickettorideUK"] = "OLFT-TTOL-UKUK",
        ["tickettorideFrance"] = "OLFT-TTOL-FRAN",
        ["tickettorideGermany"] = "OLFT-TTOL-GERM",
        ["tickettorideOW"] = "OLFT-TTOL-OWOW",
        ["tickettorideMiniUSA"] = "OLFT-TTOL-MIUS",
        ["tickettoridePolska"] = "OLFT-TTOL-POLS",
    };

    /// <summary>The product code for one feature, or null for a feature that isn't this game's.</summary>
    public static string? ForFeature(string feature)
    {
        var underscore = feature.IndexOf('_', StringComparison.Ordinal);
        var product = underscore < 0 ? feature : feature[..underscore];
        var platform = underscore < 0 ? "" : feature[(underscore + 1)..];

        if (string.Equals(product, BaseGame, StringComparison.OrdinalIgnoreCase))
        {
            return BasePlatforms.TryGetValue(platform, out var code) ? code : BasePlatforms["Standalone"];
        }

        // For maps the platform suffix doesn't matter.
        return Maps.GetValueOrDefault(product);
    }

    public static IReadOnlyList<AccessRight> ForFeatures(IEnumerable<string> features) =>
        features
            .Select(ForFeature)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(code => new AccessRight(code, code))
            .ToList();
}
