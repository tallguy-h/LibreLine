// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Players;

/// <summary>Someone on a buddy or ignore list. The name is kept so players who never logged in can be shown.</summary>
public sealed record ContactEntry(int AccountId, string Name);

/// <summary>Everything the server keeps about one player. Persisted through <see cref="IPlayerStore"/>.</summary>
public sealed class PlayerRecord
{
    public const int StartingKarma = 50;
    public const double StartingRating = 1500;

    public required int AccountId { get; init; }

    public required string Name { get; set; }

    /// <summary>The SteamID64 from <c>AsyncAuth.device</c>, refreshed at every login.</summary>
    public string? SteamId { get; set; }

    public string? Language { get; set; }

    public string? TimeZone { get; set; }

    /// <summary>Asmodee <c>features</c> cached at the last login. Never used to allow or refuse anything.</summary>
    public List<string> Features { get; set; } = [];

    public int Karma { get; set; } = StartingKarma;

    public double Rating { get; set; } = StartingRating;

    public int GamesPlayed { get; set; }

    public List<ContactEntry> Buddies { get; set; } = [];

    public List<ContactEntry> Ignores { get; set; } = [];

    /// <summary>True once the buddy list has been filled from Asmodee (on the first login).</summary>
    public bool BuddiesImported { get; set; }

    public DateTimeOffset? LastLogin { get; set; }
}

/// <summary>The storage contract for player records. Works per record so a database can replace the JSON files.</summary>
public interface IPlayerStore
{
    Task<PlayerRecord?> GetAsync(int accountId, CancellationToken cancellationToken = default);

    Task SaveAsync(PlayerRecord player, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerRecord>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>A player store in memory only (for tests, or a server that keeps nothing).</summary>
public sealed class InMemoryPlayerStore : IPlayerStore
{
    private readonly Dictionary<int, PlayerRecord> players = [];
    private readonly Lock gate = new();

    public Task<PlayerRecord?> GetAsync(int accountId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return Task.FromResult(players.GetValueOrDefault(accountId));
        }
    }

    public Task SaveAsync(PlayerRecord player, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            players[player.AccountId] = player;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlayerRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<PlayerRecord>>(players.Values.ToList());
        }
    }
}
