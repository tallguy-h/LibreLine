// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Engine;
using LibreLine.Rules.Maps;

namespace LibreLine.Rules;

/// <summary>The outcome of rebuilding a game: the model, and the first illegal step if there was one.</summary>
/// <param name="Game">The model up to the last legal step; null if the game couldn't be set up at all.</param>
public sealed record ReplayResult(RulesGame? Game, RuleViolation? Violation)
{
    public bool IsLegal => Game is not null && Violation is null;
}

public interface IRulesEngine
{
    IMapCatalog Maps { get; }

    /// <summary>True if games may be created with these settings: a known map at its rules engine version.</summary>
    bool Supports(int mapEnum, int rulesEngineVersion);

    /// <summary>Rebuilds a game from its settings, the opening choices made so far, and its moves.</summary>
    ReplayResult Replay(GameSettings settings, IReadOnlyList<OpeningChoice> openings, IReadOnlyList<Move> moves);
}

public sealed class RulesEngine(IMapCatalog maps) : IRulesEngine
{
    public IMapCatalog Maps { get; } = maps;

    public bool Supports(int mapEnum, int rulesEngineVersion) =>
        Maps.Find(mapEnum) is { } map && map.Rules.RulesEngineVersion == rulesEngineVersion;

    public ReplayResult Replay(GameSettings settings, IReadOnlyList<OpeningChoice> openings, IReadOnlyList<Move> moves)
    {
        var map = Maps.Find(settings.MapEnum);
        if (map is null)
        {
            return new ReplayResult(null, new RuleViolation(-1, 0, $"unknown map {settings.MapEnum}"));
        }

        RulesGame game;
        try
        {
            game = new RulesGame(map, settings);
        }
        catch (RuleException e)
        {
            return new ReplayResult(null, new RuleViolation(-1, 0, e.Message));
        }

        // Germany's requests are dealt in seat order, so apply every request before any choice.
        foreach (var opening in openings.OrderBy(o => o.Seat))
        {
            if (opening.Ask is { } ask)
            {
                if (Fail(-1, opening.Seat, () => game.ApplyOpeningAsk(ask with { Player = opening.Seat })) is { } v)
                {
                    return new ReplayResult(game, v);
                }
            }
        }

        foreach (var opening in openings.OrderBy(o => o.Seat))
        {
            if (opening.Choose is { } choose)
            {
                if (Fail(-1, opening.Seat, () => game.ApplyOpeningChoice(choose with { Player = opening.Seat })) is { } v)
                {
                    return new ReplayResult(game, v);
                }
            }
        }

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (Fail(i, move.Player, () => game.Apply(move)) is { } v)
            {
                return new ReplayResult(game, v);
            }
        }

        return new ReplayResult(game, null);
    }

    private static RuleViolation? Fail(int index, int seat, Action step)
    {
        try
        {
            step();
            return null;
        }
        catch (RuleException e)
        {
            return new RuleViolation(index, seat, e.Message);
        }
    }
}
