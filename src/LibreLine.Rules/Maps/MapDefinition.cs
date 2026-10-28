// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Rules.Maps;

/// <summary>Train card and route colours, numbered as on the wire (<c>pb.MapInfo.Card.Color</c>).</summary>
public enum CardColour
{
    Purple = 1,
    White = 2,
    Blue = 3,
    Yellow = 4,
    Orange = 5,
    Black = 6,
    Red = 7,
    Green = 8,
    Locomotive = 9,
}

/// <summary>A route's colour: one of the card colours, grey (any one colour) or blank (France: needs a track piece).</summary>
public enum RouteColour
{
    Purple = 1,
    White = 2,
    Blue = 3,
    Yellow = 4,
    Orange = 5,
    Black = 6,
    Red = 7,
    Green = 8,
    Grey = 9,
    Blank = 10,
}

/// <summary>Where locomotives may be played on a claim.</summary>
public enum LocomotiveUse
{
    AnyRoute,
    TunnelsOnly,
    TunnelsAndFerries,
}

public sealed record City(int Id, string Name, string? Region);

public sealed record Route(
    int Id,
    int From,
    int To,
    int Length,
    RouteColour Colour,
    int Ferry,
    bool Tunnel,
    int Mountains,
    IReadOnlyList<int> Crosses,
    bool ParallelOpen,
    IReadOnlyList<int> Shares)
{
    /// <summary>The train pieces a claim uses: its length plus its mountains (Asia).</summary>
    public int Trains => Length + Mountains;

    public bool Touches(int city) => From == city || To == city;
}

public sealed record Destination(int City, int Points);

public sealed record Ticket(int Id, int From, IReadOnlyList<Destination> Destinations, bool IsLong);

public sealed record TrackPiece(int Id, CardColour Colour, int Length);

public sealed record Railroad(int Id, string Name, string Short, IReadOnlyList<int> Payouts);

/// <summary>UK technologies, by wire id.</summary>
public enum TechnologyId
{
    IrelandFranceConcession = 0,
    ScotlandConcession = 1,
    WalesConcession = 2,
    MechanicalStoker = 3,
    BoilerLagging = 4,
    Booster = 5,
    Propellers = 6,
    SuperheatedSteamBoiler = 7,
    SteamTurbine = 8,
    DoubleHeading = 9,
    RightOfWay = 10,
    Thermocompressor = 11,
    EqualisingBeam = 12,
    RiskyContracts = 13,
    WaterTenders = 14,
    DieselPower = 15,
}

public sealed record Technology(int Id, string Name, int Cost, int Copies, bool AdvancedOnly)
{
    public TechnologyId Kind => (TechnologyId)Id;

    public bool SingleUse => Kind is TechnologyId.RightOfWay or TechnologyId.Thermocompressor;
}

/// <summary>The rules that vary between maps (each map file's Rules table).</summary>
public sealed record MapRules
{
    public int MinPlayers { get; init; } = 2;
    public int MaxPlayers { get; init; } = 5;
    public int Trains { get; init; } = 45;
    public int Stations { get; init; }
    public int HandCards { get; init; } = 4;
    public int HandLocomotives { get; init; }

    public int OpeningShort { get; init; }
    public int OpeningLong { get; init; }
    public int OpeningKeep { get; init; }
    public int? OpeningMaxLong { get; init; }

    /// <summary>Germany: players choose how to split their tickets between the two decks.</summary>
    public bool ChosenSplit { get; init; }

    /// <summary>Germany: how many opening tickets a player asks for in all.</summary>
    public int OpeningTotal { get; init; }

    public int DrawCount { get; init; } = 3;
    public int DrawKeep { get; init; } = 1;

    public bool UnkeptOpeningLeaveGame { get; init; }
    public bool ReturnedLeaveGame { get; init; }

    /// <summary>With this many players or fewer, only one side of a double route can be taken.</summary>
    public int DoubleRouteMaxPlayers { get; init; } = 3;

    public LocomotiveUse Locomotives { get; init; } = LocomotiveUse.AnyRoute;

    /// <summary>UK: how many cards of any colour count as one locomotive (0 = not allowed).</summary>
    public int LocomotiveSubstitute { get; init; }

    public bool FaceUpLocomotiveEndsTurn { get; init; } = true;
    public int FaceUpRedealAt { get; init; } = 3;
    public int TunnelReveal { get; init; } = 3;
    public int MountainPoints { get; init; }

    public int LongestPathBonus { get; init; }
    public int MostTicketsBonus { get; init; }
    public int ExplorerBonus { get; init; }

    public bool Mandala { get; init; }
    public bool StockShares { get; init; }
    public bool TrackPieces { get; init; }
    public bool Technologies { get; init; }

    public int RulesEngineVersion { get; init; }
}

/// <summary>One playable map enum: a board with one ticket set and its rules.</summary>
public sealed class MapDefinition
{
    public required int Enum { get; init; }
    public required string Name { get; init; }
    public required string ShortName { get; init; }
    public required MapRules Rules { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Route> Routes { get; init; }
    public required IReadOnlyList<Ticket> Tickets { get; init; }

    /// <summary>Each train card's colour, by card id.</summary>
    public required IReadOnlyList<CardColour> Cards { get; init; }

    public IReadOnlyList<TrackPiece> TrackPieces { get; init; } = [];
    public IReadOnlyList<Railroad> Railroads { get; init; } = [];
    public IReadOnlyList<Technology> Technologies { get; init; } = [];

    /// <summary>India: the bonus for 1, 2, ... tickets that count; the last entry applies to any higher number.</summary>
    public IReadOnlyList<int> MandalaBonus { get; init; } = [];

    /// <summary>Routes joining the same two cities as each route (double and triple routes), by route id.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Parallels => parallels ??= BuildParallels();

    /// <summary>Routes drawn across each route, both ways round, by route id.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Crossings => crossings ??= BuildCrossings();

    private IReadOnlyList<IReadOnlyList<int>>? parallels;
    private IReadOnlyList<IReadOnlyList<int>>? crossings;

    public City? FindCity(string name) => Cities.FirstOrDefault(c => c.Name == name);

    private IReadOnlyList<IReadOnlyList<int>> BuildParallels()
    {
        var result = new List<IReadOnlyList<int>>(Routes.Count);
        foreach (var route in Routes)
        {
            result.Add(Routes
                .Where(o => o.Id != route.Id
                    && ((o.From == route.From && o.To == route.To) || (o.From == route.To && o.To == route.From)))
                .Select(o => o.Id)
                .ToList());
        }

        return result;
    }

    private IReadOnlyList<IReadOnlyList<int>> BuildCrossings()
    {
        var sets = Routes.Select(_ => new SortedSet<int>()).ToList();
        foreach (var route in Routes)
        {
            foreach (var other in route.Crosses)
            {
                if (other >= 0 && other < Routes.Count)
                {
                    sets[route.Id].Add(other);
                    sets[other].Add(route.Id);
                }
            }
        }

        return sets.Select(s => (IReadOnlyList<int>)s.ToList()).ToList();
    }
}
