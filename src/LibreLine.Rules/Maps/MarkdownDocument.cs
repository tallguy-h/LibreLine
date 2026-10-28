// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Rules.Maps;

/// <summary>A Markdown table: its header and its rows, each cell trimmed.</summary>
internal sealed record MarkdownTable(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    public string First => Header.Count > 0 ? Header[0] : "";

    public int Column(string name)
    {
        for (var i = 0; i < Header.Count; i++)
        {
            if (Header[i] == name)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>A level-2 section of a Markdown file and the tables in it.</summary>
internal sealed record MarkdownSection(string Heading, IReadOnlyList<MarkdownTable> Tables);

/// <summary>Just enough Markdown to read the map files: <c>##</c> headings and pipe tables.</summary>
internal static class MarkdownDocument
{
    public static IReadOnlyList<MarkdownSection> Parse(string text)
    {
        var sections = new List<MarkdownSection>();
        var heading = "";
        var tables = new List<MarkdownTable>();
        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add(new MarkdownSection(heading, tables));
                heading = line[3..].Trim();
                tables = [];
                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            var block = new List<string>();
            while (i < lines.Length && lines[i].Trim().StartsWith('|'))
            {
                block.Add(lines[i].Trim());
                i++;
            }

            i--;
            if (block.Count >= 2)
            {
                var rows = block.Skip(2).Select(SplitRow).ToList();
                tables.Add(new MarkdownTable(SplitRow(block[0]), rows));
            }
        }

        sections.Add(new MarkdownSection(heading, tables));
        return sections;
    }

    private static IReadOnlyList<string> SplitRow(string line)
    {
        var inner = line.Trim();
        if (inner.StartsWith('|'))
        {
            inner = inner[1..];
        }

        if (inner.EndsWith('|'))
        {
            inner = inner[..^1];
        }

        return inner.Split('|').Select(c => c.Trim()).ToList();
    }
}
