// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Engine;
using LibreLine.Rules.Maps;

namespace LibreLine.Rules.Tests;

/// <summary>Whole games on every map, played with legal moves only: each must end and score.</summary>
public sealed class PlayoutTests
{
    private static readonly MapCatalog Catalog = MapCatalog.LoadEmbedded();
    private static readonly RulesEngine Engine = new(Catalog);

    public static TheoryData<int, int> Games()
    {
        var data = new TheoryData<int, int>();
        foreach (var map in Catalog.All)
        {
            for (var players = map.Rules.MinPlayers; players <= map.Rules.MaxPlayers; players++)
            {
                data.Add(map.Enum, players);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void AGamePlaysToTheEnd(int mapEnum, int players)
    {
        var map = Catalog.Find(mapEnum)!;
        var game = PlayoutBot.NewGame(map, 1000 + mapEnum * 10 + players, players);
        var moves = PlayoutBot.PlayOut(game);

        Assert.True(game.IsOver, $"{map.Name} with {players} players stopped after {moves.Count} moves at {game.Phase}");
        var scores = game.Score();
        Assert.Equal(players, scores.Count);
        Assert.Contains(scores, s => s.Rank == 1);
        Assert.All(scores, s => Assert.InRange(s.Rank, 1, players));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void AReplayRebuildsTheSameGame(int mapEnum, int players)
    {
        var map = Catalog.Find(mapEnum)!;
        var seed = 2000 + mapEnum * 10 + players;
        var played = PlayoutBot.NewGame(map, seed, players);
        var moves = PlayoutBot.PlayOut(played);

        var openings = played.Players.Select(p => new OpeningChoice(
            p.Seat,
            map.Rules.ChosenSplit ? new Move { Type = MoveType.AskForFirstTickets, NbrShort = 2, NbrLong = 2 } : null,
            new Move { Type = MoveType.ChooseFirstTicketsPrivate, Tickets = OpeningKept(played, p) })).ToList();
        var result = Engine.Replay(played.Settings, openings, moves);

        Assert.Null(result.Violation);
        Assert.Equal(played.Score().Select(s => s.Total), result.Game!.Score().Select(s => s.Total));
    }

    private static List<int> OpeningKept(RulesGame played, PlayerState player)
    {
        // The bot keeps every opening ticket it may, so they are the first ones the player holds.
        var fresh = PlayoutBot.NewGame(played.Map, played.Settings.Seed, played.Settings.Players);
        return fresh.Player(player.Seat).Tickets.ToList();
    }

    [Fact]
    public void ReplayReportsTheFirstIllegalMove()
    {
        var map = Catalog.Find(4)!;
        var game = PlayoutBot.NewGame(map, 7, 2);
        var openings = game.Players
            .Select(p => new OpeningChoice(p.Seat, null, new Move { Type = MoveType.ChooseFirstTicketsPrivate, Tickets = p.Tickets.ToList() }))
            .ToList();
        var moves = new List<Move>
        {
            new() { Type = MoveType.PickDeckTrainCard, Player = 1 },
            new() { Type = MoveType.PickDeckTrainCard, Player = 1 },
            new() { Type = MoveType.PickDeckTrainCard, Player = 1 },
        };

        var result = Engine.Replay(game.Settings, openings, moves);
        Assert.Equal(2, result.Violation!.MoveIndex);
        Assert.Equal(2, result.Game!.MovesApplied);
    }

    [Fact]
    public void SupportsOnlyEachMapsRulesEngineVersion()
    {
        Assert.True(Engine.Supports(4, 0));
        Assert.False(Engine.Supports(4, 1));
        Assert.True(Engine.Supports(9, 1));
        Assert.True(Engine.Supports(14, 2));
        Assert.False(Engine.Supports(17, 0));
    }
}
