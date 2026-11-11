// SPDX-License-Identifier: GPL-2.0-only
using System.Net;
using System.Text.RegularExpressions;

namespace LibreLine.Desktop.Game;

/// <summary>
/// Reads and changes the <c>customAsyncServerName</c> line of the game's <c>UserDefault.xml</c>. The file starts
/// with <c>&lt;?1.0?&gt;</c>, which isn't well-formed XML, so it is edited as text: everything but that one line
/// stays exactly as the game wrote it.
/// </summary>
internal static partial class GameSettingsText
{
    public const string Element = "customAsyncServerName";

    /// <summary>The configured server, or null if the game uses its built-in one.</summary>
    public static string? GetServer(string text)
    {
        var match = ValueElement().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>Sets the server, replacing an existing line or adding one straight after <c>&lt;userDefaultRoot&gt;</c>.</summary>
    /// <exception cref="FormatException">The file has no <c>userDefaultRoot</c> element.</exception>
    public static string SetServer(string text, string server)
    {
        var element = $"<{Element}>{WebUtility.HtmlEncode(server)}</{Element}>";
        if (AnyElement().IsMatch(text))
        {
            return AnyElement().Replace(text, element, 1);
        }

        var root = RootStart().Match(text);
        if (!root.Success)
        {
            throw new FormatException("The game's settings file has no <userDefaultRoot> element.");
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var after = root.Index + root.Length;
        var indent = IndentAfter().Match(text, after);
        var indentation = indent.Success && indent.Index == after && indent.Groups["indent"].Length > 0
            ? indent.Groups["indent"].Value
            : "    ";
        return text.Insert(after, newline + indentation + element);
    }

    /// <summary>Removes the setting, and its line if it had one to itself.</summary>
    public static string RemoveServer(string text)
    {
        var line = ElementLine().Match(text);
        if (line.Success)
        {
            return text.Remove(line.Index, line.Length);
        }

        return AnyElement().Replace(text, "", 1);
    }

    /// <summary>
    /// The server address in what a player pasted: just the address, the settings line, or a whole invitation. The
    /// first word with a port (<c>host:2445</c>) wins, then the first word that looks like an address.
    /// </summary>
    public static string? ExtractServer(string pasted)
    {
        var text = pasted.Trim();
        if (IsValidServer(text))
        {
            return text;
        }

        var element = ValueElement().Match(text);
        if (element.Success && IsValidServer(element.Groups["value"].Value.Trim()))
        {
            return element.Groups["value"].Value.Trim();
        }

        var words = text.Split((char[])[' ', '\t', '\r', '\n', ',', ';', '(', ')', '"', '\''],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.TrimEnd('.', '!', '?'))
            .Where(IsValidServer)
            .ToList();
        return words.FirstOrDefault(w => w.Contains(':', StringComparison.Ordinal))
            ?? words.FirstOrDefault(w => w.Contains('.', StringComparison.Ordinal));
    }

    /// <summary>True for a host name or address with an optional port, as the game accepts it.</summary>
    public static bool IsValidServer(string server) => ServerName().IsMatch(server);

    [GeneratedRegex(@"<customAsyncServerName\s*>(?<value>[^<]*)</customAsyncServerName\s*>")]
    private static partial Regex ValueElement();

    [GeneratedRegex(@"<customAsyncServerName\s*>[^<]*</customAsyncServerName\s*>|<customAsyncServerName\s*/>")]
    private static partial Regex AnyElement();

    [GeneratedRegex(@"^[ \t]*(?:<customAsyncServerName\s*>[^<]*</customAsyncServerName\s*>|<customAsyncServerName\s*/>)[ \t]*(?:\r?\n|$)", RegexOptions.Multiline)]
    private static partial Regex ElementLine();

    [GeneratedRegex(@"<userDefaultRoot(?:\s[^>]*)?>")]
    private static partial Regex RootStart();

    [GeneratedRegex(@"\G\r?\n(?<indent>[ \t]*)")]
    private static partial Regex IndentAfter();

    [GeneratedRegex(@"^(?:[A-Za-z0-9](?:[A-Za-z0-9\-\.]*[A-Za-z0-9])?|\[[0-9A-Fa-f:\.]+\])(?::[0-9]{1,5})?$")]
    private static partial Regex ServerName();
}
