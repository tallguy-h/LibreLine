// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Players;

/// <summary>How karma changes. The figures come from an old player guide and haven't been checked with the client.</summary>
public interface IKarmaPolicy
{
    int AfterFinishing(int karma);

    int AfterWithdrawing(int karma);
}

public sealed class DefaultKarmaPolicy : IKarmaPolicy
{
    public const int Maximum = 100;

    public int AfterFinishing(int karma) => Math.Min(Maximum, karma + 1);

    public int AfterWithdrawing(int karma) => Math.Max(0, karma - 5);
}

/// <summary>How ratings change after a rated game.</summary>
public interface IRatingPolicy
{
    /// <summary>The new ratings, in the same order. A lower place is better; equal places are a draw.</summary>
    IReadOnlyList<double> Update(IReadOnlyList<(double Rating, int Place)> players);
}

/// <summary>Multiplayer Elo: every pair of players is a game, scaled by the number of opponents.</summary>
public sealed class EloRatingPolicy(double k = 32) : IRatingPolicy
{
    public IReadOnlyList<double> Update(IReadOnlyList<(double Rating, int Place)> players)
    {
        var n = players.Count;
        if (n < 2)
        {
            return players.Select(p => p.Rating).ToList();
        }

        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            var delta = 0.0;
            for (var j = 0; j < n; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var expected = 1 / (1 + Math.Pow(10, (players[j].Rating - players[i].Rating) / 400));
                var actual = players[i].Place < players[j].Place ? 1.0 : players[i].Place == players[j].Place ? 0.5 : 0.0;
                delta += actual - expected;
            }

            result[i] = players[i].Rating + (k * delta / (n - 1));
        }

        return result;
    }
}
