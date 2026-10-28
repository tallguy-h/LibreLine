// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Maps;

namespace LibreLine.Rules.Scoring;

/// <summary>What scoring needs to know about one seat at the end of a game.</summary>
public sealed record ScoringInput(
    int Seat,
    IReadOnlyList<int> Routes,
    IReadOnlyList<int> Tickets,
    IReadOnlyList<int> StationCities,
    int StationsLeft,
    IReadOnlyList<int> Shares,
    IReadOnlyCollection<int> Technologies,
    int BoilerLaggingClaims,
    int SteamTurbineFerries)
{
    public static ScoringInput For(int seat, IReadOnlyList<int> routes, IReadOnlyList<int>? tickets = null) =>
        new(seat, routes, tickets ?? [], [], 0, [], [], 0, 0);
}

/// <summary>One seat's final score and how it was made up.</summary>
public sealed record PlayerScore
{
    public required int Seat { get; init; }
    public int RoutePoints { get; init; }
    public int TicketPoints { get; init; }
    public int CompletedTickets { get; init; }
    public int LongestPath { get; init; }
    public int LongestPathBonus { get; init; }
    public int MostTicketsBonus { get; init; }
    public int ExplorerBonus { get; init; }
    public int MandalaBonus { get; init; }
    public int StationPoints { get; init; }
    public int MountainPoints { get; init; }
    public int SharePoints { get; init; }
    public int TechnologyPoints { get; init; }
    public int Rank { get; init; }

    public int Total =>
        RoutePoints + TicketPoints + LongestPathBonus + MostTicketsBonus + ExplorerBonus + MandalaBonus
        + StationPoints + MountainPoints + SharePoints + TechnologyPoints;
}

/// <summary>End-of-game scoring shared by every map, with each map's bonuses.</summary>
public static class Scorer
{
    private static readonly int[] RouteTable = [0, 1, 2, 4, 7, 10, 15, 18, 21, 27, 40];

    public const int UnusedStationPoints = 4;

    public static int RoutePoints(int length) =>
        length >= 0 && length < RouteTable.Length ? RouteTable[length] : throw new ArgumentOutOfRangeException(nameof(length));

    /// <remarks>Pennsylvania's dummy shareholder never competes for payouts, so it isn't an input.</remarks>
    public static IReadOnlyList<PlayerScore> Score(MapDefinition map, IReadOnlyList<ScoringInput> players)
    {
        var rules = map.Rules;
        var owners = new Dictionary<int, List<int>>();
        foreach (var player in players)
        {
            foreach (var route in player.Routes)
            {
                if (!owners.TryGetValue(route, out var list))
                {
                    owners[route] = list = [];
                }

                list.Add(player.Seat);
            }
        }

        var scores = new List<PlayerScore>();
        foreach (var player in players)
        {
            var routes = player.Routes.Select(r => map.Routes[r]).ToList();
            var (ticketPoints, completed, completedIds) = BestTickets(map, player, owners);
            var longest = rules.LongestPathBonus > 0 || player.Technologies.Contains((int)TechnologyId.EqualisingBeam)
                ? Graph.LongestPath(routes)
                : 0;

            var technologyPoints = 0;
            if (player.Technologies.Contains((int)TechnologyId.BoilerLagging))
            {
                technologyPoints += player.BoilerLaggingClaims;
            }

            if (player.Technologies.Contains((int)TechnologyId.SteamTurbine))
            {
                technologyPoints += 2 * player.SteamTurbineFerries;
            }

            if (player.Technologies.Contains((int)TechnologyId.DoubleHeading))
            {
                technologyPoints += 2 * completed;
            }

            scores.Add(new PlayerScore
            {
                Seat = player.Seat,
                RoutePoints = routes.Sum(r => RoutePoints(r.Length)),
                TicketPoints = ticketPoints,
                CompletedTickets = completed,
                LongestPath = longest,
                StationPoints = rules.Stations > 0 ? player.StationsLeft * UnusedStationPoints : 0,
                MountainPoints = routes.Sum(r => r.Mountains) * rules.MountainPoints,
                MandalaBonus = rules.Mandala ? MandalaBonus(map, routes, completedIds) : 0,
                TechnologyPoints = technologyPoints,
            });
        }

        scores = AwardBonuses(map, players, scores);
        return Rank(scores);
    }

    private static List<PlayerScore> AwardBonuses(MapDefinition map, IReadOnlyList<ScoringInput> players, List<PlayerScore> scores)
    {
        var rules = map.Rules;
        var maxLongest = scores.Max(s => s.LongestPath);
        var maxCompleted = scores.Max(s => s.CompletedTickets);
        var explorer = rules.ExplorerBonus > 0
            ? players.Select(p => Graph.LargestNetwork(p.Routes.Select(r => map.Routes[r]))).ToList()
            : [];
        var maxExplorer = explorer.Count > 0 ? explorer.Max() : 0;
        var shares = rules.StockShares ? SharePayouts(map, players) : null;

        return scores.Select((score, i) =>
        {
            var technologies = players[i].Technologies;
            var bonusTechnology = 0;
            if (technologies.Contains((int)TechnologyId.EqualisingBeam))
            {
                bonusTechnology += score.LongestPath == maxLongest && maxLongest > 0 ? 15 : -15;
            }

            if (technologies.Contains((int)TechnologyId.RiskyContracts))
            {
                bonusTechnology += score.CompletedTickets == maxCompleted ? 20 : -20;
            }

            return score with
            {
                LongestPathBonus = rules.LongestPathBonus > 0 && maxLongest > 0 && score.LongestPath == maxLongest
                    ? rules.LongestPathBonus
                    : 0,
                MostTicketsBonus = rules.MostTicketsBonus > 0 && score.CompletedTickets == maxCompleted
                    ? rules.MostTicketsBonus
                    : 0,
                ExplorerBonus = rules.ExplorerBonus > 0 && maxExplorer > 0 && explorer[i] == maxExplorer
                    ? rules.ExplorerBonus
                    : 0,
                SharePoints = shares?[i] ?? 0,
                TechnologyPoints = score.TechnologyPoints + bonusTechnology,
            };
        }).ToList();
    }

    /// <summary>Ranks follow the scores; ties go to most completed tickets, then the longest path.</summary>
    private static List<PlayerScore> Rank(List<PlayerScore> scores)
    {
        static (int, int, int) Key(PlayerScore s) => (s.Total, s.CompletedTickets, s.LongestPath);

        return scores
            .Select(s => s with { Rank = 1 + scores.Count(o => Key(o).CompareTo(Key(s)) > 0) })
            .ToList();
    }

    /// <summary>
    /// The best ticket score a player can make, choosing for each station which other player's route into its city
    /// to borrow.
    /// </summary>
    private static (int Points, int Completed, List<int> CompletedIds) BestTickets(MapDefinition map, ScoringInput player,
        Dictionary<int, List<int>> owners)
    {
        var options = player.StationCities
            .Select(city => map.Routes
                .Where(r => r.Touches(city) && owners.TryGetValue(r.Id, out var o) && o.Any(seat => seat != player.Seat)
                    && !player.Routes.Contains(r.Id))
                .Select(r => (int?)r.Id)
                .Prepend(null)
                .ToList())
            .ToList();

        (int Points, int Completed, List<int> Ids) best = (int.MinValue, 0, []);
        foreach (var borrowed in Combinations(options))
        {
            var edges = player.Routes.Concat(borrowed.OfType<int>()).Select(r => map.Routes[r]);
            var result = EvaluateTickets(map, player.Tickets, edges);
            if (result.Points > best.Points || (result.Points == best.Points && result.Completed > best.Completed))
            {
                best = result;
            }
        }

        return best;
    }

    private static IEnumerable<int?[]> Combinations(List<List<int?>> options)
    {
        var choice = new int[options.Count];
        while (true)
        {
            yield return choice.Select((c, i) => options[i][c]).ToArray();
            var position = 0;
            while (position < options.Count)
            {
                choice[position]++;
                if (choice[position] < options[position].Count)
                {
                    break;
                }

                choice[position] = 0;
                position++;
            }

            if (position == options.Count)
            {
                yield break;
            }
        }
    }

    internal static (int Points, int Completed, List<int> Ids) EvaluateTickets(MapDefinition map, IEnumerable<int> tickets,
        IEnumerable<Route> routes)
    {
        var network = new UnionFind(map.Cities.Count);
        foreach (var route in routes)
        {
            network.Union(route.From, route.To);
        }

        var points = 0;
        var completed = new List<int>();
        foreach (var ticketId in tickets)
        {
            var ticket = map.Tickets[ticketId];
            var connected = ticket.Destinations.Where(d => network.Connected(ticket.From, d.City)).ToList();
            if (connected.Count > 0)
            {
                points += connected.Max(d => d.Points);
                completed.Add(ticketId);
            }
            else
            {
                points -= ticket.Destinations.Min(d => d.Points);
            }
        }

        return (points, completed.Count, completed);
    }

    /// <summary>India: tickets whose cities are joined by two paths sharing no route.</summary>
    private static int MandalaBonus(MapDefinition map, List<Route> routes, List<int> completed)
    {
        var count = 0;
        foreach (var ticketId in completed)
        {
            var ticket = map.Tickets[ticketId];
            if (ticket.Destinations.Any(d => Graph.EdgeDisjointPaths(routes, ticket.From, d.City, 2) >= 2))
            {
                count++;
            }
        }

        if (count == 0 || map.MandalaBonus.Count == 0)
        {
            return 0;
        }

        return map.MandalaBonus[Math.Min(count, map.MandalaBonus.Count) - 1];
    }

    /// <summary>Pennsylvania: each railroad pays its shareholders by position; tied players share a position.</summary>
    private static int[] SharePayouts(MapDefinition map, IReadOnlyList<ScoringInput> players)
    {
        var points = new int[players.Count];
        foreach (var railroad in map.Railroads)
        {
            var holdings = players.Select(p => railroad.Id < p.Shares.Count ? p.Shares[railroad.Id] : 0).ToList();
            for (var i = 0; i < players.Count; i++)
            {
                if (holdings[i] == 0)
                {
                    continue;
                }

                var position = holdings.Count(h => h > holdings[i]);
                if (position < railroad.Payouts.Count)
                {
                    points[i] += railroad.Payouts[position];
                }
            }
        }

        return points;
    }
}
