// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Maps;
using LibreLine.Rules.Scoring;

namespace LibreLine.Rules.Tests;

public sealed class ScoringTests
{
    private static readonly MapCatalog Catalog = MapCatalog.LoadEmbedded();

    private static int Route(MapDefinition map, string from, string to) =>
        map.Routes.First(r =>
        {
            var (a, b) = (map.Cities[r.From].Name, map.Cities[r.To].Name);
            return (a == from && b == to) || (a == to && b == from);
        }).Id;

    private static int Ticket(MapDefinition map, string from, string to) =>
        map.Tickets.First(t => map.Cities[t.From].Name == from && t.Destinations.Any(d => map.Cities[d.City].Name == to)).Id;

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 7)]
    [InlineData(5, 10)]
    [InlineData(6, 15)]
    [InlineData(7, 18)]
    [InlineData(8, 21)]
    [InlineData(9, 27)]
    [InlineData(10, 40)]
    public void RoutesScoreByLength(int length, int points)
    {
        Assert.Equal(points, Scorer.RoutePoints(length));
    }

    [Fact]
    public void CompletedTicketsAddAndOthersSubtract()
    {
        var usa = Catalog.Find(4)!;
        var routes = new[] { Route(usa, "Denver", "Santa Fe"), Route(usa, "Santa Fe", "El Paso") };
        var done = Ticket(usa, "Denver", "El Paso");
        var missed = Ticket(usa, "Kansas City", "Houston");
        var score = Scorer.Score(usa, [ScoringInput.For(1, routes, [done, missed]), ScoringInput.For(2, [])])[0];

        Assert.Equal(2 + 2, score.RoutePoints);
        Assert.Equal(4 - 5, score.TicketPoints);
        Assert.Equal(1, score.CompletedTickets);
    }

    [Fact]
    public void TheLongestPathBonusGoesToEveryoneTied()
    {
        var usa = Catalog.Find(4)!;
        var one = new[] { Route(usa, "Denver", "Santa Fe"), Route(usa, "Santa Fe", "El Paso") };
        var two = new[] { Route(usa, "Duluth", "Omaha") };
        var three = new[] { Route(usa, "Atlanta", "Raleigh"), Route(usa, "Raleigh", "Charleston") };
        var scores = Scorer.Score(usa, [ScoringInput.For(1, one), ScoringInput.For(2, two), ScoringInput.For(3, three)]);

        Assert.Equal([4, 2, 4], scores.Select(s => s.LongestPath));
        Assert.Equal([10, 0, 10], scores.Select(s => s.LongestPathBonus));
    }

    [Fact]
    public void ALongestPathUsesEachRouteOnceButMayRevisitCities()
    {
        // A triangle with a tail: Kansas City - Omaha - Denver - Kansas City, then Denver - Santa Fe.
        var usa = Catalog.Find(4)!;
        var routes = new[]
        {
            usa.Routes[Route(usa, "Kansas City", "Omaha")], usa.Routes[Route(usa, "Omaha", "Denver")],
            usa.Routes[Route(usa, "Denver", "Kansas City")], usa.Routes[Route(usa, "Denver", "Santa Fe")],
        };
        Assert.Equal(1 + 4 + 4 + 2, Graph.LongestPath(routes));
    }

    [Fact]
    public void TheMostTicketsBonusCountsEvenAtZero()
    {
        var usa1910 = Catalog.Find(5)!;
        var scores = Scorer.Score(usa1910, [ScoringInput.For(1, []), ScoringInput.For(2, [])]);
        Assert.All(scores, s => Assert.Equal(15, s.MostTicketsBonus));
        Assert.All(scores, s => Assert.Equal(0, s.LongestPathBonus));
    }

    [Fact]
    public void AMultiDestinationTicketScoresTheBestConnectedOrLosesTheLowest()
    {
        var swiss = Catalog.Find(1)!;
        var ticket = swiss.Tickets.First(t => t.Destinations.Count > 1);
        var best = ticket.Destinations.OrderBy(d => d.Points).First();
        var path = PathBetween(swiss, ticket.From, best.City);

        var connected = Scorer.Score(swiss, [ScoringInput.For(1, path, [ticket.Id]), ScoringInput.For(2, [])])[0];
        Assert.True(connected.TicketPoints >= best.Points);
        Assert.Equal(1, connected.CompletedTickets);

        var missed = Scorer.Score(swiss, [ScoringInput.For(1, [], [ticket.Id]), ScoringInput.For(2, [])])[0];
        Assert.Equal(-ticket.Destinations.Min(d => d.Points), missed.TicketPoints);
    }

    /// <summary>A shortest chain of routes (by count) between two cities.</summary>
    private static List<int> PathBetween(MapDefinition map, int from, int to, ISet<int>? avoid = null) =>
        TryPathBetween(map, from, to, avoid) ?? throw new InvalidOperationException("no path");

    private static List<int>? TryPathBetween(MapDefinition map, int from, int to, ISet<int>? avoid = null)
    {
        var via = new Dictionary<int, int> { [from] = -1 };
        var queue = new Queue<int>([from]);
        while (queue.Count > 0)
        {
            var city = queue.Dequeue();
            foreach (var route in map.Routes.Where(r => r.Touches(city) && avoid?.Contains(r.Id) != true))
            {
                var next = route.From == city ? route.To : route.From;
                if (via.TryAdd(next, route.Id))
                {
                    queue.Enqueue(next);
                }
            }
        }

        if (!via.ContainsKey(to))
        {
            return null;
        }

        var path = new List<int>();
        for (var at = to; at != from;)
        {
            var route = map.Routes[via[at]];
            path.Add(route.Id);
            at = route.From == at ? route.To : route.From;
        }

        return path;
    }

    [Fact]
    public void AStationBorrowsOneOtherPlayersRoute()
    {
        var europe = Catalog.Find(2)!;
        var ticket = europe.Tickets.First(t => !t.IsLong);
        var path = PathBetween(europe, ticket.From, ticket.Destinations[0].City);
        if (path.Count < 2)
        {
            return;
        }

        // Seat 1 owns all but the first route of the path; seat 2 owns that one; seat 1 has a station at its end.
        var first = europe.Routes[path[0]];
        var station = first.From;
        var mine = path.Skip(1).ToList();
        var withStation = new ScoringInput(1, mine, [ticket.Id], [station], 2, [], [], 0, 0);
        var scores = Scorer.Score(europe, [withStation, ScoringInput.For(2, [first.Id])]);
        Assert.Equal(1, scores[0].CompletedTickets);
        Assert.Equal(2 * Scorer.UnusedStationPoints, scores[0].StationPoints);
    }

    [Fact]
    public void AsiaScoresMountainsAndTheExplorerBonus()
    {
        var asia = Catalog.Find(0)!;
        var mountain = asia.Routes.First(r => r.Mountains == 1);
        var scores = Scorer.Score(asia, [ScoringInput.For(1, [mountain.Id]), ScoringInput.For(2, [])]);
        Assert.Equal(2, scores[0].MountainPoints);
        Assert.Equal(10, scores[0].ExplorerBonus);
        Assert.Equal(0, scores[1].ExplorerBonus);
    }

    [Fact]
    public void IndiaPaysTheMandalaBonusForTwoSeparatePaths()
    {
        var india = Catalog.Find(3)!;

        // A ticket joined by one chain of routes, then by a second chain that shares none of them.
        var (ticket, first, second) = india.Tickets
            .Select(t =>
            {
                var path = PathBetween(india, t.From, t.Destinations[0].City);
                var avoid = path.Concat(path.SelectMany(r => india.Parallels[r])).ToHashSet();
                return (Ticket: t, First: path, Second: TryPathBetween(india, t.From, t.Destinations[0].City, avoid));
            })
            .First(x => x.Second is not null);

        var single = Scorer.Score(india, [ScoringInput.For(1, first, [ticket.Id]), ScoringInput.For(2, [])])[0];
        Assert.Equal(1, single.CompletedTickets);
        Assert.Equal(0, single.MandalaBonus);

        var mandala = Scorer.Score(india, [ScoringInput.For(1, [.. first, .. second!], [ticket.Id]), ScoringInput.For(2, [])])[0];
        Assert.Equal(5, mandala.MandalaBonus);
    }

    [Fact]
    public void PennsylvaniaPaysShareholdersByPosition()
    {
        var pa = Catalog.Find(9)!;
        var shares = (int prr, int bo) =>
        {
            var list = new int[pa.Railroads.Count];
            list[8] = prr;
            list[7] = bo;
            return list;
        };
        var scores = Scorer.Score(pa,
        [
            new ScoringInput(1, [], [], [], 0, shares(3, 1), [], 0, 0),
            new ScoringInput(2, [], [], [], 0, shares(1, 1), [], 0, 0),
            new ScoringInput(3, [], [], [], 0, shares(0, 0), [], 0, 0),
        ]);

        // PRR: 30 to the leader, 21 to second. B&O: tied for first, both paid first place.
        Assert.Equal(30 + 20, scores[0].SharePoints);
        Assert.Equal(21 + 20, scores[1].SharePoints);
        Assert.Equal(0, scores[2].SharePoints);
    }

    [Fact]
    public void UkTechnologiesScore()
    {
        var uk = Catalog.Find(15)!;
        var holder = new ScoringInput(1, [], [], [], 0, [],
            [(int)TechnologyId.BoilerLagging, (int)TechnologyId.SteamTurbine, (int)TechnologyId.DoubleHeading,
             (int)TechnologyId.RiskyContracts],
            BoilerLaggingClaims: 3, SteamTurbineFerries: 2);
        var scores = Scorer.Score(uk, [holder, ScoringInput.For(2, [])]);

        // Boiler Lagging +3, Steam Turbine +4, Double Heading +0, Risky Contracts +20 (tied for most at zero).
        Assert.Equal(3 + 4 + 20, scores[0].TechnologyPoints);
    }

    [Fact]
    public void RanksFollowScoresThenTicketsThenLongestPath()
    {
        var usa = Catalog.Find(4)!;
        var a = ScoringInput.For(1, [Route(usa, "Denver", "Santa Fe")]);
        var b = ScoringInput.For(2, [Route(usa, "Duluth", "Omaha")]);
        var scores = Scorer.Score(usa, [a, b, ScoringInput.For(3, [Route(usa, "Seattle", "Helena")])]);
        Assert.Equal(1, scores[2].Rank);
        Assert.Equal(2, scores[0].Rank);
        Assert.Equal(2, scores[1].Rank);
    }
}
