// SPDX-License-Identifier: GPL-2.0-only
using System.Globalization;
using System.Text.RegularExpressions;

namespace LibreLine.Rules.Maps;

/// <summary>Reads one map document (docs/maps/*.md) into a <see cref="MapDefinition"/> per map enum.</summary>
internal static partial class MapFileParser
{
    public static IReadOnlyList<MapDefinition> Parse(string fileName, string text)
    {
        var sections = MarkdownDocument.Parse(text);
        var all = sections.SelectMany(s => s.Tables.Select(t => (Section: s, Table: t))).ToList();

        var enumTable = Require(all, fileName, t => t.First == "Map enum");
        var enums = enumTable.Rows.Select(r => (Enum: Int(r[0]), Name: r[1], Short: r[2])).ToList();

        var rulesTable = Require(all, fileName, t => t.First == "Rule");
        var cities = ParseCities(Require(all, fileName, t => t.First == "Id" && t.Column("City") == 1));
        var cityIds = cities.ToDictionary(c => c.Name, c => c.Id, StringComparer.Ordinal);
        var cards = ParseCards(Require(all, fileName, t => t.First == "Card ids"));

        var railroads = Optional(all, t => t.First == "Wire id") is { } railroadTable ? ParseRailroads(railroadTable) : [];
        var routes = ParseRoutes(fileName,
            Require(all, fileName, t => t.First == "Id" && t.Column("From") == 1 && t.Column("Length") >= 0),
            cityIds, railroads);
        var trackPieces = Optional(all, t => t.First == "Ids" && t.Column("Length") >= 0) is { } pieceTable
            ? ParseTrackPieces(pieceTable)
            : [];
        var technologies = Optional(all, t => t.First == "Id" && t.Column("Technology") == 1) is { } techTable
            ? ParseTechnologies(techTable)
            : [];
        var mandala = Optional(all, t => t.First == "Tickets" && t.Rows.Count > 0 && t.Rows[0][0] == "Bonus") is { } mandalaTable
            ? mandalaTable.Rows[0].Skip(1).Select(Int).ToList()
            : [];

        var ticketSets = all
            .Where(x => x.Section.Heading.StartsWith("Tickets", StringComparison.Ordinal) && x.Table.First == "Id")
            .Select(x => (Enums: TicketEnums(fileName, x.Section.Heading), Tickets: ParseTickets(fileName, x.Table, cityIds)))
            .ToList();

        var maps = new List<MapDefinition>();
        for (var column = 0; column < enums.Count; column++)
        {
            var (mapEnum, name, shortName) = enums[column];
            var tickets = ticketSets.FirstOrDefault(s => s.Enums.Contains(mapEnum)).Tickets
                ?? throw new FormatException($"{fileName}: no ticket set for map enum {mapEnum}");
            var rules = ParseRules(fileName, rulesTable, column);
            var advanced = name.Contains("Advanced", StringComparison.Ordinal);

            maps.Add(new MapDefinition
            {
                Enum = mapEnum,
                Name = name,
                ShortName = shortName,
                Rules = rules,
                Cities = cities,
                Routes = routes,
                Tickets = tickets,
                Cards = cards,
                TrackPieces = trackPieces,
                Railroads = railroads,
                Technologies = rules.Technologies ? technologies.Where(t => advanced || !t.AdvancedOnly).ToList() : [],
                MandalaBonus = mandala,
            });
        }

        return maps;
    }

    private static MarkdownTable Require(
        List<(MarkdownSection Section, MarkdownTable Table)> all, string fileName, Func<MarkdownTable, bool> match) =>
        Optional(all, match) ?? throw new FormatException($"{fileName}: a required table is missing");

    private static MarkdownTable? Optional(List<(MarkdownSection Section, MarkdownTable Table)> all, Func<MarkdownTable, bool> match) =>
        all.Select(x => x.Table).FirstOrDefault(match);

    private static List<City> ParseCities(MarkdownTable table)
    {
        var cities = table.Rows
            .Select(row => new City(Int(row[0]), row[1], row.Count > 2 && row[2].Length > 0 ? row[2] : null))
            .ToList();
        CheckIds(cities.Select(c => c.Id), "city");
        return cities;
    }

    private static List<CardColour> ParseCards(MarkdownTable table)
    {
        var cards = new List<CardColour>();
        foreach (var row in table.Rows)
        {
            var (from, to) = Range(row[0]);
            if (from != cards.Count)
            {
                throw new FormatException($"train card ids don't follow on at {row[0]}");
            }

            var colour = Colour(row[1]);
            for (var id = from; id <= to; id++)
            {
                cards.Add(colour);
            }
        }

        return cards;
    }

    private static List<Route> ParseRoutes(string fileName, MarkdownTable table, Dictionary<string, int> cityIds,
        IReadOnlyList<Railroad> railroads)
    {
        var notesColumn = table.Column("Notes");
        var sharesColumn = table.Column("Shares");
        var routes = new List<Route>();
        foreach (var row in table.Rows)
        {
            var id = Int(row[0]);
            var ferry = 0;
            var tunnel = false;
            var mountains = 0;
            var parallelOpen = false;
            var crosses = new List<int>();

            foreach (var note in SplitNotes(notesColumn >= 0 ? row[notesColumn] : ""))
            {
                Match m;
                if ((m = FerryNote().Match(note)).Success)
                {
                    ferry = Int(m.Groups[1].Value);
                }
                else if (note == "tunnel")
                {
                    tunnel = true;
                }
                else if ((m = MountainNote().Match(note)).Success)
                {
                    mountains = Int(m.Groups[1].Value);
                }
                else if (note == "parallel route open")
                {
                    parallelOpen = true;
                }
                else if ((m = CrossesNote().Match(note)).Success)
                {
                    crosses.AddRange(m.Groups[1].Value.Split(',').Select(s => Int(s)));
                }
                else
                {
                    throw new FormatException($"{fileName}: route {id}: unknown note '{note}'");
                }
            }

            var shares = new List<int>();
            if (sharesColumn >= 0 && row[sharesColumn].Length > 0 && row[sharesColumn] != "none")
            {
                foreach (var code in row[sharesColumn].Split(',').Select(s => s.Trim()))
                {
                    var railroad = railroads.FirstOrDefault(r => r.Short == code)
                        ?? throw new FormatException($"{fileName}: route {id}: unknown railroad '{code}'");
                    shares.Add(railroad.Id);
                }
            }

            routes.Add(new Route(
                id,
                CityId(fileName, cityIds, row[1]),
                CityId(fileName, cityIds, row[2]),
                Int(row[3]),
                RouteColourOf(row[4]),
                ferry,
                tunnel,
                mountains,
                crosses,
                parallelOpen,
                shares));
        }

        CheckIds(routes.Select(r => r.Id), "route");
        return routes;
    }

    /// <summary>Splits a notes cell on commas, keeping "crosses 3, 4" together.</summary>
    private static IEnumerable<string> SplitNotes(string notes)
    {
        string? crosses = null;
        foreach (var part in notes.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            if (crosses is not null && int.TryParse(part, CultureInfo.InvariantCulture, out _))
            {
                crosses += ", " + part;
                continue;
            }

            if (crosses is not null)
            {
                yield return crosses;
                crosses = null;
            }

            if (part.StartsWith("crosses ", StringComparison.Ordinal))
            {
                crosses = part;
            }
            else
            {
                yield return part;
            }
        }

        if (crosses is not null)
        {
            yield return crosses;
        }
    }

    private static List<Ticket> ParseTickets(string fileName, MarkdownTable table, Dictionary<string, int> cityIds)
    {
        var notesColumn = table.Column("Notes");
        var tickets = new List<Ticket>();
        foreach (var row in table.Rows)
        {
            var id = Int(row[0]);
            var to = row[2].Split(" / ").Select(s => s.Trim()).ToList();
            var points = row[3].Split(" / ").Select(Int).ToList();
            if (to.Count != points.Count)
            {
                throw new FormatException($"{fileName}: ticket {id}: destinations and points don't match");
            }

            var destinations = to.Select((city, i) => new Destination(CityId(fileName, cityIds, city), points[i])).ToList();
            var isLong = notesColumn >= 0 && row[notesColumn] == "long";
            tickets.Add(new Ticket(id, CityId(fileName, cityIds, row[1]), destinations, isLong));
        }

        CheckIds(tickets.Select(t => t.Id), "ticket");
        return tickets;
    }

    private static List<TrackPiece> ParseTrackPieces(MarkdownTable table)
    {
        var pieces = new List<TrackPiece>();
        foreach (var row in table.Rows)
        {
            var (from, to) = Range(row[0]);
            for (var id = from; id <= to; id++)
            {
                pieces.Add(new TrackPiece(id, Colour(row[1]), Int(row[2])));
            }
        }

        CheckIds(pieces.Select(p => p.Id), "track piece");
        return pieces;
    }

    private static List<Railroad> ParseRailroads(MarkdownTable table)
    {
        var railroads = table.Rows
            .Select(r => new Railroad(Int(r[0]), r[1], r[2], r[3].Split(',').Select(Int).ToList()))
            .ToList();
        CheckIds(railroads.Select(r => r.Id), "railroad");
        return railroads;
    }

    private static List<Technology> ParseTechnologies(MarkdownTable table)
    {
        var technologies = table.Rows
            .Select(r => new Technology(Int(r[0]), r[1], Int(r[2]), Int(r[3]),
                r[4].StartsWith("UK Advanced", StringComparison.Ordinal)))
            .ToList();
        CheckIds(technologies.Select(t => t.Id), "technology");
        return technologies;
    }

    private static int[] TicketEnums(string fileName, string heading)
    {
        var m = EnumsInHeading().Match(heading);
        return m.Success
            ? m.Groups[1].Value.Split(',').Select(Int).ToArray()
            : throw new FormatException($"{fileName}: ticket heading '{heading}' names no map enum");
    }

    private static MapRules ParseRules(string fileName, MarkdownTable table, int column)
    {
        string? Cell(string rule)
        {
            var row = table.Rows.FirstOrDefault(r => r[0] == rule);
            if (row is null)
            {
                return null;
            }

            // A single column covers every enum in the file; "same" repeats the column to its left.
            var index = Math.Min(column + 1, row.Count - 1);
            while (index > 1 && row[index] == "same")
            {
                index--;
            }

            return row[index];
        }

        var rules = new MapRules();

        if (Cell("Players") is { } players)
        {
            var (min, max) = Range(players);
            rules = rules with { MinPlayers = min, MaxPlayers = max };
        }

        if (Cell("Trains each") is { } trains)
        {
            rules = rules with { Trains = Int(trains) };
        }

        if (Cell("Stations each") is { } stations)
        {
            rules = rules with { Stations = Int(stations) };
        }

        if (Cell("Opening hand") is { } hand)
        {
            var locos = LocoInHand().Match(hand);
            var cards = CardsInHand().Match(hand);
            rules = rules with
            {
                HandLocomotives = locos.Success ? Int(locos.Groups[1].Value) : 0,
                HandCards = cards.Success ? Int(cards.Groups[1].Value) : throw new FormatException($"{fileName}: opening hand '{hand}'"),
            };
        }

        if (Cell("Opening tickets") is { } opening)
        {
            var parsed = ParseTicketCount(fileName, opening);
            rules = rules with
            {
                OpeningShort = parsed.Short,
                OpeningLong = parsed.Long,
                OpeningKeep = parsed.Keep,
                OpeningMaxLong = parsed.MaxLong,
                ChosenSplit = parsed.Split,
                OpeningTotal = parsed.Short + parsed.Long,
            };
        }

        if (Cell("Ticket draw") is { } draw)
        {
            var parsed = ParseTicketCount(fileName, draw);
            rules = rules with { DrawCount = parsed.Short + parsed.Long, DrawKeep = parsed.Keep };
        }

        rules = rules with
        {
            UnkeptOpeningLeaveGame = Cell("Unkept opening tickets")?.StartsWith("leave", StringComparison.Ordinal) ?? false,
            ReturnedLeaveGame = Cell("Returned tickets")?.StartsWith("leave", StringComparison.Ordinal) ?? false,
        };

        if (Cell("Double routes") is { } doubles)
        {
            var m = DoubleRoutes().Match(doubles);
            if (!m.Success)
            {
                throw new FormatException($"{fileName}: double routes '{doubles}'");
            }

            rules = rules with { DoubleRouteMaxPlayers = Int(m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value) };
        }

        if (Cell("Locomotives") is { } locomotives)
        {
            var use = locomotives.StartsWith("only on tunnels and ferries", StringComparison.Ordinal) ? LocomotiveUse.TunnelsAndFerries
                : locomotives.StartsWith("only on tunnels", StringComparison.Ordinal) ? LocomotiveUse.TunnelsOnly
                : LocomotiveUse.AnyRoute;
            var substitute = SubstituteCards().Match(locomotives);
            rules = rules with
            {
                Locomotives = use,
                LocomotiveSubstitute = substitute.Success ? Int(substitute.Groups[1].Value) : 0,
            };
        }

        if (Cell("Face-up locomotive") is { } faceUp)
        {
            rules = rules with { FaceUpLocomotiveEndsTurn = faceUp.StartsWith("whole turn", StringComparison.Ordinal) };
        }

        if (Cell("Face-up row redealt at") is { } redeal)
        {
            rules = rules with { FaceUpRedealAt = Int(LeadingNumber().Match(redeal).Value) };
        }

        if (Cell("Tunnels") is { } tunnels)
        {
            rules = rules with { TunnelReveal = Int(LeadingNumber().Match(tunnels).Value) };
        }

        if (Cell("Mountains") is { } mountains)
        {
            var m = ScoresN().Match(mountains);
            rules = rules with { MountainPoints = m.Success ? Int(m.Groups[1].Value) : 0 };
        }

        return rules with
        {
            LongestPathBonus = Bonus(Cell("Longest path bonus")),
            MostTicketsBonus = Bonus(Cell("Most tickets bonus")),
            ExplorerBonus = Bonus(Cell("Explorer bonus")),
            Mandala = Cell("Mandala bonus") is not null,
            StockShares = Cell("Stock shares") is not null,
            TrackPieces = Cell("Track pieces") is not null,
            Technologies = Cell("Technologies") is not null,
            RulesEngineVersion = Int(Cell("Rules engine version") ?? "0"),
        };
    }

    private static int Bonus(string? text)
    {
        if (text is null || text == "none")
        {
            return 0;
        }

        var m = LeadingNumber().Match(text);
        return m.Success ? Int(m.Value) : 0;
    }

    private static (int Short, int Long, int Keep, int? MaxLong, bool Split) ParseTicketCount(string fileName, string text)
    {
        var keep = KeepN().Match(text);
        if (!keep.Success)
        {
            throw new FormatException($"{fileName}: ticket count '{text}'");
        }

        var longPart = LongPlus().Match(text);
        var maxLong = AtMostLong().Match(text);
        var (shortCount, longCount) = longPart.Success
            ? (Int(ShortN().Match(text).Groups[1].Value), Int(longPart.Groups[1].Value))
            : (Int(LeadingNumber().Match(text).Value), 0);

        return (shortCount, longCount, Int(keep.Groups[1].Value), maxLong.Success ? Int(maxLong.Groups[1].Value) : null,
            text.Contains("split", StringComparison.Ordinal));
    }

    private static int CityId(string fileName, Dictionary<string, int> cityIds, string name) =>
        cityIds.TryGetValue(name, out var id) ? id : throw new FormatException($"{fileName}: unknown city '{name}'");

    private static CardColour Colour(string text) => text switch
    {
        "purple" => CardColour.Purple,
        "white" => CardColour.White,
        "blue" => CardColour.Blue,
        "yellow" => CardColour.Yellow,
        "orange" => CardColour.Orange,
        "black" => CardColour.Black,
        "red" => CardColour.Red,
        "green" => CardColour.Green,
        "locomotive" => CardColour.Locomotive,
        _ => throw new FormatException($"unknown colour '{text}'"),
    };

    private static RouteColour RouteColourOf(string text) => text switch
    {
        "grey" => RouteColour.Grey,
        "blank" or "" => RouteColour.Blank,
        _ => (RouteColour)(int)Colour(text),
    };

    private static (int From, int To) Range(string text)
    {
        var parts = text.Split('-');
        return parts.Length == 1 ? (Int(parts[0]), Int(parts[0])) : (Int(parts[0]), Int(parts[1]));
    }

    private static int Int(string text) => int.Parse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static void CheckIds(IEnumerable<int> ids, string what)
    {
        var expected = 0;
        foreach (var id in ids)
        {
            if (id != expected)
            {
                throw new FormatException($"{what} ids have a gap or are out of order at {id} (expected {expected})");
            }

            expected++;
        }
    }

    [GeneratedRegex(@"^ferry (\d+)$")]
    private static partial Regex FerryNote();

    [GeneratedRegex(@"^(\d+) mountains?$")]
    private static partial Regex MountainNote();

    [GeneratedRegex(@"^crosses ([\d, ]+)$")]
    private static partial Regex CrossesNote();

    [GeneratedRegex(@"\(enums? ([\d, ]+)\)")]
    private static partial Regex EnumsInHeading();

    [GeneratedRegex(@"(\d+) locomotive")]
    private static partial Regex LocoInHand();

    [GeneratedRegex(@"(\d+) cards")]
    private static partial Regex CardsInHand();

    [GeneratedRegex(@"keep (\d+)\+")]
    private static partial Regex KeepN();

    [GeneratedRegex(@"(\d+) long \+")]
    private static partial Regex LongPlus();

    [GeneratedRegex(@"(\d+) short")]
    private static partial Regex ShortN();

    [GeneratedRegex(@"at most (\d+) long")]
    private static partial Regex AtMostLong();

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"with (\d+)(?:-(\d+))? players")]
    private static partial Regex DoubleRoutes();

    [GeneratedRegex(@"any (\d+) cards")]
    private static partial Regex SubstituteCards();

    [GeneratedRegex(@"scores (\d+)")]
    private static partial Regex ScoresN();
}
