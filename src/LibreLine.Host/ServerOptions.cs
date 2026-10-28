// SPDX-License-Identifier: GPL-2.0-only
using System.Globalization;
using LibreLine.Games;

namespace LibreLine.Host;

/// <summary>The <c>LibreLine</c> configuration section (docs/ARCHITECTURE.md#configuration).</summary>
public sealed class ServerOptions
{
    public const string Section = "LibreLine";

    /// <summary>The TLS port the client connects to. 0 picks a free port (for tests).</summary>
    public int Port { get; set; } = 2445;

    /// <summary>Where the player files and the generated certificate go.</summary>
    public string DataDirectory { get; set; } = "data";

    public CertificateOptions Certificate { get; set; } = new();

    public AsmodeeSection Asmodee { get; set; } = new();

    /// <summary>The map enums games may be created on, as ranges: <c>0-16</c>, <c>0,2,4-7</c>.</summary>
    public string Maps { get; set; } = "0-16";

    /// <summary><c>enforce</c>, <c>audit</c> or <c>off</c>.</summary>
    public string Rules { get; set; } = "enforce";

    public sealed class CertificateOptions
    {
        public string Path { get; set; } = "";

        public string Password { get; set; } = "";
    }

    public sealed class AsmodeeSection
    {
        public string BaseUrl { get; set; } = "https://api.asmodee.net/";
    }

    public static HashSet<int> ParseMaps(string text)
    {
        var maps = new HashSet<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries);
            var from = int.Parse(bounds[0], CultureInfo.InvariantCulture);
            var to = bounds.Length > 1 ? int.Parse(bounds[1], CultureInfo.InvariantCulture) : from;
            for (var map = from; map <= to; map++)
            {
                maps.Add(map);
            }
        }

        return maps;
    }

    public static RulesMode ParseRules(string text) => text.Trim().ToLowerInvariant() switch
    {
        "enforce" => RulesMode.Enforce,
        "audit" => RulesMode.Audit,
        "off" => RulesMode.Off,
        _ => throw new FormatException($"LibreLine:Rules must be enforce, audit or off, not '{text}'"),
    };
}
