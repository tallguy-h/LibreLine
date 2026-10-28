// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Rules.Engine;

/// <summary>One seat's position in the rules model.</summary>
public sealed class PlayerState
{
    internal PlayerState(int seat, int trains, int stations, int railroads)
    {
        Seat = seat;
        Trains = trains;
        StationsLeft = stations;
        SharesList = new int[railroads];
    }

    internal PlayerState Clone()
    {
        var copy = new PlayerState(Seat, Trains, StationsLeft, SharesList.Length)
        {
            BoilerLaggingClaims = BoilerLaggingClaims,
            SteamTurbineFerries = SteamTurbineFerries,
            OpeningAsk = OpeningAsk,
            OpeningDealt = OpeningDealt,
            OpeningDone = OpeningDone,
        };
        copy.HandList.AddRange(HandList);
        copy.TicketList.AddRange(TicketList);
        copy.PendingList.AddRange(PendingList);
        copy.RouteList.AddRange(RouteList);
        copy.StationList.AddRange(StationList);
        SharesList.CopyTo(copy.SharesList, 0);
        copy.TechnologySet.UnionWith(TechnologySet);
        copy.UsedTechnologies.UnionWith(UsedTechnologies);
        return copy;
    }

    public int Seat { get; }

    public int Trains { get; internal set; }

    public int StationsLeft { get; internal set; }

    public IReadOnlyList<int> Hand => HandList;

    /// <summary>Tickets kept.</summary>
    public IReadOnlyList<int> Tickets => TicketList;

    /// <summary>Tickets dealt and not yet kept or returned (the opening, or a ticket draw).</summary>
    public IReadOnlyList<int> PendingTickets => PendingList;

    public IReadOnlyList<int> Routes => RouteList;

    public IReadOnlyList<int> StationCities => StationList;

    /// <summary>Pennsylvania: shares held, by railroad id.</summary>
    public IReadOnlyList<int> Shares => SharesList;

    /// <summary>UK: technologies held, by id.</summary>
    public IReadOnlyCollection<int> Technologies => TechnologySet;

    /// <summary>UK: routes claimed while holding Boiler Lagging.</summary>
    public int BoilerLaggingClaims { get; internal set; }

    /// <summary>UK: ferries claimed while holding Steam Turbine.</summary>
    public int SteamTurbineFerries { get; internal set; }

    /// <summary>Germany: the opening request, once made.</summary>
    public (int Short, int Long)? OpeningAsk { get; internal set; }

    public bool OpeningDealt { get; internal set; }

    public bool OpeningDone { get; internal set; }

    internal List<int> HandList { get; } = [];
    internal List<int> TicketList { get; } = [];
    internal List<int> PendingList { get; } = [];
    internal List<int> RouteList { get; } = [];
    internal List<int> StationList { get; } = [];
    internal int[] SharesList { get; }
    internal HashSet<int> TechnologySet { get; } = [];
    internal HashSet<int> UsedTechnologies { get; } = [];

    internal bool Has(Maps.TechnologyId technology) => TechnologySet.Contains((int)technology);
}
