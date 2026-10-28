// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Engine;
using LibreLine.Rules.Maps;

namespace LibreLine.Rules.Tests;

public sealed class SetupTests
{
    private static readonly MapCatalog Catalog = MapCatalog.LoadEmbedded();

    private static RulesGame Deal(int mapEnum, int seed, int players) =>
        new(Catalog.Find(mapEnum)!, new GameSettings(seed, mapEnum, players, 0, Catalog.Find(mapEnum)!.Rules.RulesEngineVersion));

    [Fact]
    public void TheSameSeedDealsTheSameGame()
    {
        var a = Deal(4, 987654, 3);
        var b = Deal(4, 987654, 3);
        Assert.Equal(a.ShortTicketDeck, b.ShortTicketDeck);
        Assert.Equal(a.FaceUp, b.FaceUp);
        Assert.Equal(a.TrainDeck, b.TrainDeck);
        Assert.All(a.Players, p => Assert.Equal(p.Hand, b.Player(p.Seat).Hand));
        Assert.All(a.Players, p => Assert.Equal(p.PendingTickets, b.Player(p.Seat).PendingTickets));
    }

    [Fact]
    public void ADifferentSeedDealsADifferentGame()
    {
        Assert.NotEqual(Deal(4, 1, 2).ShortTicketDeck, Deal(4, 2, 2).ShortTicketDeck);
    }

    [Fact]
    public void TheTicketDeckIsAShuffleInMapOrder()
    {
        // The first pick is uniform(0, n − 1) over the tickets in map order.
        var map = Catalog.Find(4)!;
        var game = Deal(4, 31337, 2);
        var random = new MinStdRandom(31337);
        var first = random.Uniform(0, map.Tickets.Count - 1);
        Assert.Equal(first, game.Player(1).PendingTickets[0]);
    }

    [Fact]
    public void DealsHandsTicketsAndTheFaceUpRow()
    {
        var game = Deal(4, 4242, 4);
        Assert.All(game.Players, p => Assert.Equal(4, p.Hand.Count));
        Assert.All(game.Players, p => Assert.Equal(3, p.PendingTickets.Count));
        Assert.Equal(5, game.FaceUp.Count(c => c is not null));
        Assert.Equal(110 - 5 - 16, game.TrainDeckCount + game.Discard.Count);
        Assert.Equal(30 - 12, game.ShortTicketDeck.Count);
        Assert.True(game.FaceUp.Count(c => game.ColourOf(c!.Value) == CardColour.Locomotive) < 3);
    }

    [Fact]
    public void TheTrainDeckStartsInIdOrderAndIsNeverShuffled()
    {
        var game = Deal(4, 77, 2);
        var dealt = game.FaceUp.OfType<int>().Concat(game.Players.SelectMany(p => p.Hand)).Concat(game.Discard).ToHashSet();
        var expected = Enumerable.Range(0, 110).Where(c => !dealt.Contains(c)).ToList();
        Assert.Equal(expected, game.TrainDeck);
    }

    [Fact]
    public void LongTicketsComeFromTheirOwnDeck()
    {
        var game = Deal(2, 555, 3);
        var map = Catalog.Find(2)!;
        Assert.All(game.Players, p =>
        {
            Assert.Equal(3, p.PendingTickets.Count(t => !map.Tickets[t].IsLong));
            Assert.Equal(1, p.PendingTickets.Count(t => map.Tickets[t].IsLong));
            Assert.True(map.Tickets[p.PendingTickets[^1]].IsLong);
        });
        Assert.Equal(6 - 3, game.LongTicketDeck.Count);
    }

    [Fact]
    public void UkPlayersEachStartWithTheLowestLocomotiveLeft()
    {
        var game = Deal(13, 2024, 3);
        Assert.Equal(96, game.Player(1).Hand[0]);
        Assert.Equal(97, game.Player(2).Hand[0]);
        Assert.Equal(98, game.Player(3).Hand[0]);
        Assert.All(game.Players, p => Assert.Equal(5, p.Hand.Count));
    }

    [Fact]
    public void GermanyDealsInSeatOrderOnceEarlierSeatsHaveAsked()
    {
        var game = Deal(14, 99, 2);
        Assert.All(game.Players, p => Assert.Empty(p.PendingTickets));

        game.ApplyOpeningAsk(new Move { Type = MoveType.AskForFirstTickets, Player = 2, NbrShort = 1, NbrLong = 3 });
        Assert.False(game.CanCheckOpeningChoice(2));

        var shortTop = game.ShortTicketDeck.Take(3).ToList();
        var longTop = game.LongTicketDeck.Take(5).ToList();
        game.ApplyOpeningAsk(new Move { Type = MoveType.AskForFirstTickets, Player = 1, NbrShort = 2, NbrLong = 2 });

        Assert.Equal([.. shortTop.Take(2), .. longTop.Take(2)], game.Player(1).PendingTickets);
        Assert.Equal([shortTop[2], .. longTop.Skip(2).Take(3)], game.Player(2).PendingTickets);
    }

    [Fact]
    public void GermanyRefusesTheWrongNumberOfTickets()
    {
        var game = Deal(14, 99, 2);
        Assert.Throws<RuleException>(() =>
            game.ApplyOpeningAsk(new Move { Type = MoveType.AskForFirstTickets, Player = 1, NbrShort = 3, NbrLong = 2 }));
    }

    [Fact]
    public void AnOpeningChoiceMustKeepEnoughDealtTickets()
    {
        var game = Deal(4, 8, 2);
        var dealt = game.Player(1).PendingTickets;
        Assert.Throws<RuleException>(() => game.ApplyOpeningChoice(Choose(1, dealt.Take(1))));
        Assert.Throws<RuleException>(() => game.ApplyOpeningChoice(Choose(1, [dealt[0], game.Player(2).PendingTickets[0]])));
        game.ApplyOpeningChoice(Choose(1, dealt.Take(2)));
        Assert.Equal(2, game.Player(1).Tickets.Count);
    }

    [Fact]
    public void UnkeptOpeningTicketsGoUnderTheDeckOrLeaveTheGame()
    {
        var usa = Deal(4, 8, 2);
        var unkept = usa.Player(1).PendingTickets[2];
        usa.ApplyOpeningChoice(Choose(1, usa.Player(1).PendingTickets.Take(2)));
        Assert.Equal(unkept, usa.ShortTicketDeck[^1]);

        var asia = Deal(0, 8, 2);
        var inDecks = asia.ShortTicketDeck.Count + asia.LongTicketDeck.Count;
        asia.ApplyOpeningChoice(Choose(1, asia.Player(1).PendingTickets.Take(2)));
        Assert.Equal(inDecks, asia.ShortTicketDeck.Count + asia.LongTicketDeck.Count);
    }

    [Fact]
    public void MegaGameKeepsAtMostOneLongTicket()
    {
        var game = Deal(11, 3, 2);
        var map = Catalog.Find(11)!;
        var longs = game.Player(1).PendingTickets.Where(t => map.Tickets[t].IsLong).ToList();
        var shorts = game.Player(1).PendingTickets.Where(t => !map.Tickets[t].IsLong).ToList();
        Assert.Equal(2, longs.Count);
        Assert.Throws<RuleException>(() => game.Clone().ApplyOpeningChoice(Choose(1, [.. shorts.Take(1), .. longs])));
        game.ApplyOpeningChoice(Choose(1, shorts.Take(3)));
    }

    [Fact]
    public void MovesWaitForTheOpening()
    {
        var game = Deal(4, 8, 2);
        Assert.Throws<RuleException>(() => game.Apply(new Move { Type = MoveType.PickDeckTrainCard, Player = 1 }));
    }

    [Fact]
    public void TheFirstPlayerComesFromTheSettings()
    {
        var map = Catalog.Find(4)!;
        Assert.Equal(3, new RulesGame(map, new GameSettings(1, 4, 3, 2, 0)).CurrentPlayer);
    }

    private static Move Choose(int seat, IEnumerable<int> tickets) =>
        new() { Type = MoveType.ChooseFirstTicketsPrivate, Player = seat, Tickets = tickets.ToList() };
}
