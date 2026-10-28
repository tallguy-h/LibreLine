// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder;
using Com.Daysofwonder.Async;
using LibreLine.Players;

namespace LibreLine.Sessions;

/// <summary>Builds the <c>Player</c> and <c>SmallPlayer</c> records the client is sent.</summary>
public sealed class PlayerMessages(IPlayerService players)
{
    /// <summary>
    /// A <c>Player</c>. <paramref name="position"/> is the 1-based place in the list being sent (a seat, a buddy
    /// list position, or 1 for a lone record); the account id goes in <c>w_w_w_id</c>.
    /// </summary>
    public Player Build(int position, int accountId, bool withRights = false, bool isBot = false)
    {
        var record = players.Find(accountId);
        var player = new Player
        {
            Id = position,
            WWWId = accountId,
            Name = record?.Name ?? players.NameOf(accountId),
            Karma = record?.Karma ?? PlayerRecord.StartingKarma,
            RankScore = record?.Rating ?? PlayerRecord.StartingRating,
            NbGames = record?.GamesPlayed ?? 0,
            Rank = players.RankOf(accountId),
            IsActive = true,
        };

        if (isBot)
        {
            player.IsBot = true;
        }

        if (withRights)
        {
            var rights = new AccessRights();
            foreach (var right in players.RightsOf(accountId))
            {
                rights.Rights.Add(new AccessRights.Types.Right { Name = right.Name, Feature = right.Feature });
            }

            player.Rights = rights;
        }

        return player;
    }

    /// <summary>A lobby list entry.</summary>
    public SmallPlayer BuildSmall(int accountId)
    {
        var record = players.Find(accountId);
        return new SmallPlayer
        {
            WWWId = accountId,
            Name = record?.Name ?? players.NameOf(accountId),
            Karma = record?.Karma ?? PlayerRecord.StartingKarma,
            RankScore = record?.Rating ?? PlayerRecord.StartingRating,
            NbGames = record?.GamesPlayed ?? 0,
            Rank = players.RankOf(accountId),
        };
    }
}
