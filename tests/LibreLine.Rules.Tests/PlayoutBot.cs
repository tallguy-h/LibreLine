// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Engine;
using LibreLine.Rules.Maps;

namespace LibreLine.Rules.Tests;

/// <summary>
/// A simple player for tests: claims the longest route it can pay for, otherwise draws. It proposes moves; the model
/// says which are legal, so a game played by it exercises every rule a real game would reach.
/// </summary>
internal static class PlayoutBot
{
    public static RulesGame NewGame(MapDefinition map, int seed, int players, int firstPlayer = 0)
    {
        var game = new RulesGame(map, new GameSettings(seed, map.Enum, players, firstPlayer, map.Rules.RulesEngineVersion));
        if (map.Rules.ChosenSplit)
        {
            foreach (var player in game.Players)
            {
                game.ApplyOpeningAsk(new Move { Type = MoveType.AskForFirstTickets, Player = player.Seat, NbrShort = 2, NbrLong = 2 });
            }
        }

        foreach (var player in game.Players)
        {
            game.ApplyOpeningChoice(new Move
            {
                Type = MoveType.ChooseFirstTicketsPrivate,
                Player = player.Seat,
                Tickets = KeepForOpening(game, player),
            });
        }

        return game;
    }

    private static List<int> KeepForOpening(RulesGame game, PlayerState player)
    {
        var maxLong = game.Map.Rules.OpeningMaxLong ?? int.MaxValue;
        var kept = player.PendingTickets.Where(t => !game.Map.Tickets[t].IsLong).ToList();
        kept.AddRange(player.PendingTickets.Where(t => game.Map.Tickets[t].IsLong).Take(maxLong));
        return kept;
    }

    /// <summary>Plays until the game ends or <paramref name="maxMoves"/> moves; returns the moves played.</summary>
    public static List<Move> PlayOut(RulesGame game, int maxMoves = 3000)
    {
        var moves = new List<Move>();
        while (!game.IsOver && moves.Count < maxMoves)
        {
            var move = NextMove(game);
            if (move is null)
            {
                break;
            }

            game.Apply(move);
            moves.Add(move);
        }

        return moves;
    }

    public static Move? NextMove(RulesGame game)
    {
        var seat = game.CurrentPlayer;
        var player = game.Player(seat);
        Move M(MoveType type) => new() { Type = type, Player = seat };

        switch (game.Phase)
        {
            case TurnPhase.SecondCard:
                return Draw(game, seat);
            case TurnPhase.ReturnTickets:
                return M(MoveType.ReturnTicketsPrivate);
            case TurnPhase.DigTunnel:
                return DigTunnel(game, player);
            case TurnPhase.OwnShare:
                return M(MoveType.BuyStockShare) with { Railroad = game.Map.Routes[game.PendingRoute!.Value].Shares[0] };
            case TurnPhase.DummyShare:
            case TurnPhase.OptionalDummyShare:
                return M(MoveType.BuyStockShare) with
                {
                    Railroad = game.Map.Routes[game.PendingRoute!.Value].Shares[^1],
                    ForDummy = true,
                };
            case TurnPhase.TrackPiece:
                return TrackPiece(game, seat) ?? M(MoveType.PassTurnWithoutPuttingTrackPieceOnRoute);
            case TurnPhase.EndOfTurn:
                return BuyTechnology(game, player) ?? M(MoveType.RemoveSingleUseTechnologies);
            case TurnPhase.Start:
                if (BuyTechnology(game, player) is { } technology)
                {
                    return technology;
                }

                return Claim(game, player)
                    ?? Draw(game, seat)
                    ?? (game.IsLegal(Tickets(game, seat)) ? Tickets(game, seat) : null);
            default:
                return null;
        }
    }

    private static Move Tickets(RulesGame game, int seat) => new()
    {
        Type = MoveType.AskForTickets,
        Player = seat,
        NbrShort = game.Map.Rules.ChosenSplit ? Math.Min(game.ShortTicketDeck.Count, game.Map.Rules.DrawCount) : game.Map.Rules.DrawCount,
        NbrLong = game.Map.Rules.ChosenSplit
            ? Math.Min(game.LongTicketDeck.Count, game.Map.Rules.DrawCount - Math.Min(game.ShortTicketDeck.Count, game.Map.Rules.DrawCount))
            : 0,
    };

    private static Move? Draw(RulesGame game, int seat)
    {
        var deck = new Move { Type = MoveType.PickDeckTrainCard, Player = seat };
        if (game.IsLegal(deck))
        {
            return deck;
        }

        for (var slot = 1; slot <= 5; slot++)
        {
            var open = new Move { Type = MoveType.PickOpenTrainCard, Player = seat, Slot = slot };
            if (game.IsLegal(open))
            {
                return open;
            }
        }

        return null;
    }

    private static Move? Claim(RulesGame game, PlayerState player)
    {
        foreach (var route in game.Map.Routes.OrderByDescending(r => r.Length).ThenBy(r => r.Id))
        {
            if (route.Trains > player.Trains || game.Owners(route.Id).Count > 0)
            {
                continue;
            }

            foreach (var cards in Payments(game, player, route))
            {
                var move = new Move { Type = MoveType.ClaimRoute, Player = player.Seat, Route = route.Id, Cards = cards };
                if (game.IsLegal(move))
                {
                    return move;
                }
            }
        }

        return null;
    }

    /// <summary>Candidate payments: as many cards of one colour as possible, topped up with locomotives.</summary>
    private static IEnumerable<List<int>> Payments(RulesGame game, PlayerState player, Route route)
    {
        var colour = route.Colour == RouteColour.Blank && game.PaintedPiece(route.Id) is { } piece
            ? (RouteColour)(int)game.Map.TrackPieces[piece].Colour
            : route.Colour;
        var locos = player.Hand.Where(c => game.ColourOf(c) == CardColour.Locomotive).ToList();
        var colours = colour == RouteColour.Grey
            ? Enum.GetValues<CardColour>().Where(c => c != CardColour.Locomotive)
            : colour == RouteColour.Blank ? [] : [(CardColour)(int)colour];

        foreach (var c in colours)
        {
            var matching = player.Hand.Where(card => game.ColourOf(card) == c).ToList();
            var coloured = Math.Min(matching.Count, route.Length - route.Ferry);
            var needLocos = route.Length - coloured;
            if (needLocos <= locos.Count)
            {
                yield return [.. matching.Take(coloured), .. locos.Take(needLocos)];
            }
        }

        if (locos.Count >= route.Length)
        {
            yield return locos.Take(route.Length).ToList();
        }
    }

    private static Move DigTunnel(RulesGame game, PlayerState player)
    {
        var extra = player.Hand
            .Where(c => game.ColourOf(c) == CardColour.Locomotive || game.ColourOf(c) == game.TunnelColour)
            .OrderBy(c => game.ColourOf(c) == CardColour.Locomotive ? 1 : 0)
            .Take(game.TunnelExtraCost)
            .ToList();
        return new Move
        {
            Type = MoveType.DigTunnel,
            Player = player.Seat,
            Cards = extra.Count == game.TunnelExtraCost ? extra : [],
        };
    }

    private static Move? TrackPiece(RulesGame game, int seat)
    {
        foreach (var route in game.Map.Routes.Where(r => r.Colour == RouteColour.Blank))
        {
            foreach (var piece in game.Map.TrackPieces.Where(p => p.Length == route.Length && game.PieceInSupply(p.Id)))
            {
                var move = new Move { Type = MoveType.PutTrackPieceOnRoute, Player = seat, Route = route.Id, TrackPiece = piece.Id };
                if (game.IsLegal(move))
                {
                    return move;
                }
            }
        }

        return null;
    }

    /// <summary>UK: buys the technologies a claim needs, paying with locomotives.</summary>
    private static Move? BuyTechnology(RulesGame game, PlayerState player)
    {
        if (!game.Map.Rules.Technologies || game.BoughtTechnologyThisTurn)
        {
            return null;
        }

        TechnologyId[] wanted =
        [
            TechnologyId.MechanicalStoker, TechnologyId.SuperheatedSteamBoiler, TechnologyId.ScotlandConcession,
            TechnologyId.WalesConcession, TechnologyId.IrelandFranceConcession, TechnologyId.Propellers,
        ];
        var locos = player.Hand.Where(c => game.ColourOf(c) == CardColour.Locomotive).ToList();
        foreach (var id in wanted)
        {
            var technology = game.Map.Technologies.First(t => t.Kind == id);
            if (player.Technologies.Contains(technology.Id) || locos.Count < technology.Cost)
            {
                continue;
            }

            var move = new Move
            {
                Type = MoveType.PickTechnology,
                Player = player.Seat,
                Technology = technology.Id,
                Cards = locos.Take(technology.Cost).ToList(),
            };
            if (game.IsLegal(move))
            {
                return move;
            }
        }

        return null;
    }
}
