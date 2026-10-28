// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Engine;
using LibreLine.Rules.Maps;

namespace LibreLine.Rules.Tests;

public sealed class TurnTests
{
    private static readonly MapCatalog Catalog = MapCatalog.LoadEmbedded();

    private static Move Deck(int seat) => new() { Type = MoveType.PickDeckTrainCard, Player = seat };

    private static Move Open(int seat, int slot) => new() { Type = MoveType.PickOpenTrainCard, Player = seat, Slot = slot };

    [Fact]
    public void TwoCardsEndTheTurn()
    {
        var game = PlayoutBot.NewGame(Catalog.Find(4)!, 1, 2);
        game.Apply(Deck(1));
        Assert.Equal(TurnPhase.SecondCard, game.Phase);
        Assert.Equal(1, game.CurrentPlayer);
        game.Apply(Deck(1));
        Assert.Equal(2, game.CurrentPlayer);
        Assert.Equal(6, game.Player(1).Hand.Count);
    }

    [Fact]
    public void OnlyTheSeatToMoveMayAct()
    {
        var game = PlayoutBot.NewGame(Catalog.Find(4)!, 1, 2);
        Assert.Throws<RuleException>(() => game.Apply(Deck(2)));
    }

    [Fact]
    public void TakingAFaceUpCardRefillsItsSlotFromTheDeck()
    {
        var game = PlayoutBot.NewGame(Catalog.Find(4)!, 3, 2);
        var slot = Enumerable.Range(1, 5).First(s => game.ColourOf(game.FaceUp[s - 1]!.Value) != CardColour.Locomotive);
        var taken = game.FaceUp[slot - 1]!.Value;
        var deckBefore = game.TrainDeckCount;
        game.Apply(Open(1, slot));
        Assert.Contains(taken, game.Player(1).Hand);
        Assert.NotNull(game.FaceUp[slot - 1]);
        Assert.True(game.TrainDeckCount < deckBefore);
    }

    [Fact]
    public void AFaceUpLocomotiveIsTheWholeTurnOnMostMaps()
    {
        var (game, slot) = FindFaceUpLocomotive(Catalog.Find(4)!);
        game.Apply(Open(1, slot));
        Assert.Equal(2, game.CurrentPlayer);
    }

    [Fact]
    public void AFaceUpLocomotiveCantBeTheSecondCard()
    {
        var (game, slot) = FindFaceUpLocomotive(Catalog.Find(4)!);
        game.Apply(Deck(1));
        if (game.ColourOf(game.FaceUp[slot - 1]!.Value) == CardColour.Locomotive)
        {
            Assert.Throws<RuleException>(() => game.Apply(Open(1, slot)));
        }
    }

    [Fact]
    public void SwitzerlandTreatsAFaceUpLocomotiveAsAnOrdinaryCard()
    {
        var (game, slot) = FindFaceUpLocomotive(Catalog.Find(1)!);
        game.Apply(Open(1, slot));
        Assert.Equal(TurnPhase.SecondCard, game.Phase);
        Assert.Equal(1, game.CurrentPlayer);
    }

    private static (RulesGame Game, int Slot) FindFaceUpLocomotive(MapDefinition map)
    {
        for (var seed = 1; ; seed++)
        {
            var game = PlayoutBot.NewGame(map, seed, 2);
            for (var slot = 1; slot <= 5; slot++)
            {
                if (game.ColourOf(game.FaceUp[slot - 1]!.Value) == CardColour.Locomotive)
                {
                    return (game, slot);
                }
            }
        }
    }

    [Fact]
    public void PaymentsCoverTheRouteExactly()
    {
        var map = Catalog.Find(4)!;
        var game = PlayoutBot.NewGame(map, 5, 2);
        var player = game.Player(1);
        var grey2 = map.Routes.First(r => r.Colour == RouteColour.Grey && r.Length == 2);
        var purple = Enumerable.Range(0, 12).ToList();
        var locos = Enumerable.Range(96, 14).ToList();

        Assert.Equal(CardColour.Purple, game.CheckRoutePayment(grey2, grey2.Colour, purple.Take(2).ToList(), player));
        Assert.Null(game.CheckRoutePayment(grey2, grey2.Colour, locos.Take(2).ToList(), player));
        Assert.Equal(CardColour.Purple, game.CheckRoutePayment(grey2, grey2.Colour, [purple[0], locos[0]], player));
        Assert.Throws<RuleException>(() => game.CheckRoutePayment(grey2, grey2.Colour, purple.Take(3).ToList(), player));
        Assert.Throws<RuleException>(() => game.CheckRoutePayment(grey2, grey2.Colour, [0, 12], player));
    }

    [Fact]
    public void FerriesNeedLocomotives()
    {
        var map = Catalog.Find(2)!;
        var game = PlayoutBot.NewGame(map, 5, 2);
        var ferry = map.Routes.First(r => r.Ferry == 1 && r.Length == 2 && r.Colour == RouteColour.Grey);
        Assert.Throws<RuleException>(() => game.CheckRoutePayment(ferry, ferry.Colour, [0, 1], game.Player(1)));
        Assert.Equal(CardColour.Purple, game.CheckRoutePayment(ferry, ferry.Colour, [0, 96], game.Player(1)));
    }

    [Fact]
    public void SwitzerlandAllowsLocomotivesOnlyOnTunnels()
    {
        var map = Catalog.Find(1)!;
        var game = PlayoutBot.NewGame(map, 5, 2);
        var plain = map.Routes.First(r => !r.Tunnel && r.Colour == RouteColour.Grey && r.Length >= 2);
        var tunnel = map.Routes.First(r => r.Tunnel && r.Colour == RouteColour.Grey && r.Length >= 2);
        var cards = Enumerable.Range(0, plain.Length - 1).Append(96).ToList();
        Assert.Throws<RuleException>(() => game.CheckRoutePayment(plain, plain.Colour, cards, game.Player(1)));
        var tunnelCards = Enumerable.Range(0, tunnel.Length - 1).Append(96).ToList();
        Assert.Equal(CardColour.Purple, game.CheckRoutePayment(tunnel, tunnel.Colour, tunnelCards, game.Player(1)));
    }

    [Fact]
    public void UkCountsFourCardsAsALocomotive()
    {
        var map = Catalog.Find(13)!;
        var game = PlayoutBot.NewGame(map, 5, 2);
        var route = map.Routes.First(r => r.Length == 2 && r.Colour == RouteColour.Grey && r.Ferry == 0);

        // Two purple cards, or one purple plus four of any colour standing in for a locomotive.
        Assert.Equal(CardColour.Purple, game.CheckRoutePayment(route, route.Colour, [0, 1], game.Player(1)));
        Assert.Equal(CardColour.Purple, game.CheckRoutePayment(route, route.Colour, [0, 12, 24, 36, 48], game.Player(1)));
        Assert.Throws<RuleException>(() => game.CheckRoutePayment(route, route.Colour, [0, 12, 24, 36], game.Player(1)));
    }

    [Fact]
    public void TicketDrawsKeepAtLeastOne()
    {
        var game = PlayoutBot.NewGame(Catalog.Find(4)!, 11, 2);
        game.Apply(new Move { Type = MoveType.AskForTickets, Player = 1, NbrShort = 3 });
        var drawn = game.Player(1).PendingTickets.ToList();
        Assert.Equal(3, drawn.Count);
        Assert.False(game.IsLegal(new Move { Type = MoveType.ReturnTicketsPrivate, Player = 1, Tickets = drawn }));
        game.Apply(new Move { Type = MoveType.ReturnTicketsPrivate, Player = 1, Tickets = drawn.Take(2).ToList() });
        Assert.Contains(drawn[2], game.Player(1).Tickets);
        Assert.Equal(drawn.Take(2), game.ShortTicketDeck.TakeLast(2));
        Assert.Equal(2, game.CurrentPlayer);
    }

    [Fact]
    public void ATunnelCostsOneMoreCardPerMatchingRevealedCard()
    {
        var map = Catalog.Find(1)!;
        for (var seed = 1; seed < 400; seed++)
        {
            var game = PlayoutBot.NewGame(map, seed, 2);
            for (var i = 0; i < 300 && !game.IsOver; i++)
            {
                var move = PlayoutBot.NextMove(game);
                if (move is null)
                {
                    break;
                }

                game.Apply(move);
                if (move.Type == MoveType.ClaimRoute && game.Phase == TurnPhase.DigTunnel)
                {
                    var colour = game.TunnelColour;
                    var revealed = game.RevealedTunnelCards.ToList();
                    var matching = revealed.Count(c => game.ColourOf(c) == CardColour.Locomotive || game.ColourOf(c) == colour);
                    Assert.InRange(revealed.Count, 1, 3);
                    Assert.Equal(matching, game.TunnelExtraCost);

                    // Giving up returns the base payment to the hand, and discards only the revealed cards.
                    var giveUp = game.Clone();
                    giveUp.Apply(new Move { Type = MoveType.DigTunnel, Player = move.Player });
                    Assert.All(move.Cards, c => Assert.Contains(c, giveUp.Player(move.Player).Hand));
                    Assert.Empty(giveUp.Owners(move.Route!.Value));
                    Assert.Equal(revealed, giveUp.Discard.TakeLast(revealed.Count));
                    return;
                }
            }
        }

        Assert.Fail("no tunnel with an extra cost came up");
    }

    [Fact]
    public void AClaimMustMatchTheRouteColour()
    {
        var map = Catalog.Find(4)!;
        var game = PlayoutBot.NewGame(map, 5, 2);
        var red4 = map.Routes.First(r => r.Colour == RouteColour.Red && r.Length == 4);
        Assert.Throws<RuleException>(() => game.CheckRoutePayment(red4, red4.Colour, [0, 1, 2, 3], game.Player(1)));
        Assert.Equal(CardColour.Red, game.CheckRoutePayment(red4, red4.Colour, [72, 73, 74, 96], game.Player(1)));
    }

    [Fact]
    public void ADoubleRouteHasOneSideWithTwoOrThreePlayers()
    {
        var map = Catalog.Find(4)!;
        var (first, second) = (map.Routes[1], map.Routes[2]);
        Assert.Contains(second.Id, map.Parallels[first.Id]);

        foreach (var (players, open) in new[] { (2, false), (3, false), (4, true) })
        {
            var game = PlayoutBot.NewGame(map, 5, players);
            game.GiveCards(1, [0]);
            game.Apply(new Move { Type = MoveType.ClaimRoute, Player = 1, Route = first.Id, Cards = [0] });
            game.GiveCards(2, [1]);
            Assert.Equal(open, game.IsLegal(new Move { Type = MoveType.ClaimRoute, Player = 2, Route = second.Id, Cards = [1] }));
        }
    }

    [Fact]
    public void NobodyOwnsBothSidesOfADoubleRoute()
    {
        var map = Catalog.Find(4)!;
        var game = PlayoutBot.NewGame(map, 5, 4);
        game.GiveCards(1, [0, 1]);
        game.Apply(new Move { Type = MoveType.ClaimRoute, Player = 1, Route = 1, Cards = [0] });
        for (var seat = 2; seat <= 4; seat++)
        {
            game.Apply(new Move { Type = MoveType.PickDeckTrainCard, Player = seat });
            game.Apply(new Move { Type = MoveType.PickDeckTrainCard, Player = seat });
        }

        Assert.False(game.IsLegal(new Move { Type = MoveType.ClaimRoute, Player = 1, Route = 2, Cards = [1] }));
    }

    [Fact]
    public void ClaimingUsesTrainsAndDiscardsTheCards()
    {
        var map = Catalog.Find(4)!;
        var game = PlayoutBot.NewGame(map, 5, 2);
        var route = map.Routes.First(r => r.Colour == RouteColour.Red && r.Length == 4);
        game.GiveCards(1, [72, 73, 74, 75]);
        game.Apply(new Move { Type = MoveType.ClaimRoute, Player = 1, Route = route.Id, Cards = [72, 73, 74, 75] });
        Assert.Equal(41, game.Player(1).Trains);
        Assert.Equal([1], game.Owners(route.Id));
        Assert.Equal([72, 73, 74, 75], game.Discard.TakeLast(4));
        Assert.Equal(2, game.CurrentPlayer);
    }

    [Fact]
    public void TheGameEndsAfterEveryoneHasHadOneMoreTurn()
    {
        var map = Catalog.Find(4)!;
        var game = PlayoutBot.NewGame(map, 5, 2);
        PlayoutBot.PlayOut(game);
        Assert.True(game.IsOver);
        Assert.Contains(game.Players, p => p.Trains <= 2);
    }
}
