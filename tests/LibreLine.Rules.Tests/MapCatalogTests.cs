// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Maps;

namespace LibreLine.Rules.Tests;

public sealed class MapCatalogTests
{
    private static readonly MapCatalog Catalog = MapCatalog.LoadEmbedded();

    [Fact]
    public void EveryMultiplayerMapParses()
    {
        Assert.Equal(Enumerable.Range(0, 17), Catalog.All.Select(m => m.Enum));
    }

    [Theory]
    [InlineData(0, "as")]
    [InlineData(1, "ch")]
    [InlineData(2, "eu")]
    [InlineData(4, "us")]
    [InlineData(9, "pa")]
    [InlineData(13, "uk")]
    [InlineData(15, "ua")]
    [InlineData(16, "fr")]
    public void ShortNamesMatchTheMapTable(int mapEnum, string shortName)
    {
        Assert.Equal(shortName, Catalog.Find(mapEnum)!.ShortName);
    }

    [Fact]
    public void EveryRouteAndTicketNamesRealCities()
    {
        foreach (var map in Catalog.All)
        {
            Assert.All(map.Routes, r => Assert.InRange(r.From, 0, map.Cities.Count - 1));
            Assert.All(map.Routes, r => Assert.InRange(r.To, 0, map.Cities.Count - 1));
            Assert.All(map.Tickets, t => Assert.NotEmpty(t.Destinations));
            Assert.Equal(Enumerable.Range(0, map.Routes.Count), map.Routes.Select(r => r.Id));
            Assert.Equal(Enumerable.Range(0, map.Tickets.Count), map.Tickets.Select(t => t.Id));
        }
    }

    [Fact]
    public void UsaHasTheClassicBoard()
    {
        var usa = Catalog.Find(4)!;
        Assert.Equal(36, usa.Cities.Count);
        Assert.Equal(100, usa.Routes.Count);
        Assert.Equal(30, usa.Tickets.Count);
        Assert.Equal(110, usa.Cards.Count);
        Assert.Equal(14, usa.Cards.Count(c => c == CardColour.Locomotive));
        Assert.Equal(3, usa.Rules.OpeningShort);
        Assert.Equal(2, usa.Rules.OpeningKeep);
        Assert.Equal(10, usa.Rules.LongestPathBonus);
        Assert.Equal(0, usa.Rules.MostTicketsBonus);
    }

    [Fact]
    public void TicketSetsShareTheBoardButNotTheRules()
    {
        var mega = Catalog.Find(6)!;
        Assert.Same(Catalog.Find(4)!.Routes, mega.Routes);
        Assert.Equal(69, mega.Tickets.Count);
        Assert.Equal(5, mega.Rules.OpeningShort);
        Assert.Equal(3, mega.Rules.OpeningKeep);
        Assert.Equal(4, mega.Rules.DrawCount);
        Assert.Equal(15, mega.Rules.MostTicketsBonus);
        Assert.Equal(0, Catalog.Find(5)!.Rules.LongestPathBonus);
    }

    [Fact]
    public void SwitzerlandRestrictsLocomotivesAndDiscardsTickets()
    {
        var swiss = Catalog.Find(1)!;
        Assert.Equal(LocomotiveUse.TunnelsOnly, swiss.Rules.Locomotives);
        Assert.False(swiss.Rules.FaceUpLocomotiveEndsTurn);
        Assert.True(swiss.Rules.ReturnedLeaveGame);
        Assert.Equal(2, swiss.Rules.DoubleRouteMaxPlayers);
        Assert.Equal(2, swiss.Routes.Count(r => r.ParallelOpen));
        Assert.Contains(swiss.Tickets, t => t.Destinations.Count > 1);
    }

    [Fact]
    public void NordicAllowsLocomotivesOnTunnelsAndFerries()
    {
        var nordic = Catalog.Find(8)!;
        Assert.Equal(LocomotiveUse.TunnelsAndFerries, nordic.Rules.Locomotives);
        Assert.Equal(10, nordic.Rules.MostTicketsBonus);
        Assert.Equal(40, nordic.Rules.Trains);
    }

    [Fact]
    public void EuropeHasStationsAndLongTickets()
    {
        var europe = Catalog.Find(2)!;
        Assert.Equal(3, europe.Rules.Stations);
        Assert.Equal(1, europe.Rules.OpeningLong);
        Assert.Equal(3, europe.Rules.OpeningShort);
        Assert.Equal(6, europe.Tickets.Count(t => t.IsLong));
        Assert.Equal(1, Catalog.Find(11)!.Rules.OpeningMaxLong);
        Assert.Contains(europe.Routes, r => r.Tunnel);
        Assert.Contains(europe.Routes, r => r.Ferry > 0);
    }

    [Fact]
    public void AsiaHasMountainsAndTheExplorerBonus()
    {
        var asia = Catalog.Find(0)!;
        Assert.Equal(2, asia.Rules.MountainPoints);
        Assert.Equal(10, asia.Rules.ExplorerBonus);
        Assert.Contains(asia.Routes, r => r.Mountains > 0);
        Assert.True(asia.Rules.UnkeptOpeningLeaveGame);
    }

    [Fact]
    public void IndiaHasTheMandalaTable()
    {
        var india = Catalog.Find(3)!;
        Assert.True(india.Rules.Mandala);
        Assert.Equal([5, 10, 20, 30, 40, 50], india.MandalaBonus);
    }

    [Fact]
    public void PennsylvaniaHasRailroadsAndShares()
    {
        var pa = Catalog.Find(9)!;
        Assert.True(pa.Rules.StockShares);
        Assert.Equal(1, pa.Rules.RulesEngineVersion);
        Assert.Equal(9, pa.Railroads.Count);
        Assert.Equal([30, 21, 14, 9, 6], pa.Railroads[8].Payouts);
        Assert.Equal(["PRR", "ERIE", "NYC"], pa.Routes[0].Shares.Select(s => pa.Railroads[s].Short));
        Assert.Empty(pa.Routes[28].Shares);
    }

    [Fact]
    public void UnitedKingdomHasTechnologies()
    {
        var uk = Catalog.Find(13)!;
        var advanced = Catalog.Find(15)!;
        Assert.Equal(116, uk.Cards.Count);
        Assert.Equal(1, uk.Rules.HandLocomotives);
        Assert.Equal(4, uk.Rules.LocomotiveSubstitute);
        Assert.Equal(4, uk.Rules.FaceUpRedealAt);
        Assert.Equal(2, uk.Rules.DoubleRouteMaxPlayers);
        Assert.Equal(11, uk.Technologies.Count);
        Assert.Equal(16, advanced.Technologies.Count);
        Assert.Equal(10, uk.Routes[61].Length);
        Assert.Equal("England", uk.Cities[0].Region);
    }

    [Fact]
    public void GermanyLetsPlayersChooseTheirTickets()
    {
        var germany = Catalog.Find(14)!;
        Assert.True(germany.Rules.ChosenSplit);
        Assert.Equal(4, germany.Rules.OpeningTotal);
        Assert.Equal(4, germany.Rules.DrawCount);
        Assert.Equal(2, germany.Rules.RulesEngineVersion);
        Assert.Contains(germany.Parallels, p => p.Count == 2);
    }

    [Fact]
    public void FranceHasTrackPiecesAndCrossings()
    {
        var france = Catalog.Find(16)!;
        Assert.True(france.Rules.TrackPieces);
        Assert.Equal(64, france.TrackPieces.Count);
        Assert.Equal(8, france.Rules.HandCards);
        Assert.Contains(france.Routes, r => r.Colour == RouteColour.Blank);
        var crossed = france.Routes.First(r => r.Crosses.Count > 0);
        Assert.All(crossed.Crosses, other => Assert.Contains(crossed.Id, france.Crossings[other]));
    }
}
