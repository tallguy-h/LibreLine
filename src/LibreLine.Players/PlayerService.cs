// SPDX-License-Identifier: GPL-2.0-only
using Asmodee.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LibreLine.Players;

/// <summary>A player's place in a finished rated game.</summary>
/// <param name="Withdrew">Withdrew or ran out of time: counts as last, and earns no karma for finishing.</param>
public sealed record GameResult(int AccountId, int Place, bool Withdrew);

/// <summary>What a rated game changed for one player.</summary>
public sealed record ResultChange(int AccountId, double OldRating, double NewRating, int OldGames, int NewGames,
    int OldKarma, int NewKarma);

public sealed record KarmaChange(int AccountId, int OldKarma, int NewKarma);

public interface IPlayerService
{
    /// <summary>Loads the stored players. Called once at start-up.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    int Count { get; }

    PlayerRecord? Find(int accountId);

    /// <summary>A player's name: their own record, or how a buddy or ignore list knows them.</summary>
    string NameOf(int accountId);

    /// <summary>1 + the number of players rated above this one.</summary>
    int RankOf(int accountId);

    /// <summary><c>Player.rights</c> from the entitlements cached at the player's last login (none if never).</summary>
    IReadOnlyList<AccessRight> RightsOf(int accountId);

    /// <summary>True if the player's buddy list still has to be read from Asmodee.</summary>
    bool NeedsBuddyImport(int accountId);

    /// <summary>Reads the player's Asmodee buddies, or null if Asmodee failed. An HTTP call: don't hold the server lock.</summary>
    Task<IReadOnlyList<AsmodeeBuddy>?> FetchAsmodeeBuddiesAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>Creates or refreshes the player's record at login.</summary>
    Task<PlayerRecord> RecordLoginAsync(AsmodeeUser user, string? steamId, IReadOnlyList<AsmodeeBuddy>? asmodeeBuddies,
        CancellationToken cancellationToken = default);

    Task<ContactEntry?> AddBuddyAsync(int owner, int buddy, CancellationToken cancellationToken = default);

    Task<bool> RemoveBuddyAsync(int owner, int buddy, CancellationToken cancellationToken = default);

    Task<ContactEntry?> AddIgnoreAsync(int owner, int ignored, CancellationToken cancellationToken = default);

    Task<bool> RemoveIgnoreAsync(int owner, int ignored, CancellationToken cancellationToken = default);

    bool IsIgnoring(int owner, int other);

    /// <summary>Karma lost by withdrawing or running out of time (lobby games only).</summary>
    Task<KarmaChange?> PenaliseAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>Updates ratings, games played and karma after a finished lobby game.</summary>
    Task<IReadOnlyList<ResultChange>> RecordRatedGameAsync(IReadOnlyList<GameResult> results,
        CancellationToken cancellationToken = default);
}

public static class PlayerServiceCollectionExtensions
{
    public static IServiceCollection AddPlayers(this IServiceCollection services)
    {
        services.TryAddSingleton<IKarmaPolicy, DefaultKarmaPolicy>();
        services.TryAddSingleton<IRatingPolicy>(_ => new EloRatingPolicy());
        services.TryAddSingleton<IPlayerStore, InMemoryPlayerStore>();
        services.AddSingleton<IPlayerService, PlayerService>();
        return services;
    }
}

/// <summary>
/// The player records, cached in memory and written through to the store. Callers hold the server lock, except for
/// <see cref="FetchAsmodeeBuddiesAsync"/>.
/// </summary>
public sealed class PlayerService(
    IPlayerStore store,
    IAsmodeeClient asmodee,
    IKarmaPolicy karma,
    IRatingPolicy rating,
    ILogger<PlayerService> logger) : IPlayerService
{
    private readonly Dictionary<int, PlayerRecord> players = [];

    public int Count => players.Count;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        foreach (var player in await store.ListAsync(cancellationToken))
        {
            players[player.AccountId] = player;
        }

        logger.LogInformation("Loaded {Count} players", players.Count);
    }

    public PlayerRecord? Find(int accountId) => players.GetValueOrDefault(accountId);

    public string NameOf(int accountId)
    {
        if (players.TryGetValue(accountId, out var player))
        {
            return player.Name;
        }

        foreach (var other in players.Values)
        {
            var entry = other.Buddies.Concat(other.Ignores).FirstOrDefault(c => c.AccountId == accountId);
            if (entry is not null)
            {
                return entry.Name;
            }
        }

        return $"Player {accountId}";
    }

    public int RankOf(int accountId)
    {
        var player = Find(accountId);
        var ratingValue = player?.Rating ?? PlayerRecord.StartingRating;
        return 1 + players.Values.Count(p => p.Rating > ratingValue);
    }

    public IReadOnlyList<AccessRight> RightsOf(int accountId) =>
        Find(accountId) is { } player ? ProductCodes.ForFeatures(player.Features) : [];

    public bool NeedsBuddyImport(int accountId) => Find(accountId) is not { BuddiesImported: true };

    public async Task<IReadOnlyList<AsmodeeBuddy>?> FetchAsmodeeBuddiesAsync(string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await asmodee.GetBuddiesAsync(accessToken, cancellationToken);
        }
        catch (Exception e) when (e is AsmodeeException or AsmodeeUnauthorizedException)
        {
            logger.LogWarning(e, "Couldn't read the Asmodee buddy list; it will be tried again at the next login");
            return null;
        }
    }

    public async Task<PlayerRecord> RecordLoginAsync(AsmodeeUser user, string? steamId,
        IReadOnlyList<AsmodeeBuddy>? asmodeeBuddies, CancellationToken cancellationToken = default)
    {
        if (!players.TryGetValue(user.UserId, out var player))
        {
            player = new PlayerRecord { AccountId = user.UserId, Name = user.LoginName };
            players[player.AccountId] = player;
            logger.LogInformation("New player {Account} ({Name})", player.AccountId, player.Name);
        }

        if (user.LoginName.Length > 0)
        {
            player.Name = user.LoginName;
        }

        player.SteamId = steamId ?? player.SteamId;
        player.Language = user.Language;
        player.TimeZone = user.TimeZone;
        player.Features = user.Features.ToList();
        player.LastLogin = DateTimeOffset.UtcNow;

        if (!player.BuddiesImported && asmodeeBuddies is not null)
        {
            foreach (var buddy in asmodeeBuddies)
            {
                if (buddy.Id != player.AccountId && player.Buddies.All(b => b.AccountId != buddy.Id))
                {
                    player.Buddies.Add(new ContactEntry(buddy.Id, buddy.LoginName));
                }
            }

            player.BuddiesImported = true;
            logger.LogInformation("Imported {Count} buddies for {Account}", player.Buddies.Count, player.AccountId);
        }

        await store.SaveAsync(player, cancellationToken);
        return player;
    }

    public Task<ContactEntry?> AddBuddyAsync(int owner, int buddy, CancellationToken cancellationToken = default) =>
        AddContactAsync(owner, buddy, p => p.Buddies, cancellationToken);

    public Task<bool> RemoveBuddyAsync(int owner, int buddy, CancellationToken cancellationToken = default) =>
        RemoveContactAsync(owner, buddy, p => p.Buddies, cancellationToken);

    public Task<ContactEntry?> AddIgnoreAsync(int owner, int ignored, CancellationToken cancellationToken = default) =>
        AddContactAsync(owner, ignored, p => p.Ignores, cancellationToken);

    public Task<bool> RemoveIgnoreAsync(int owner, int ignored, CancellationToken cancellationToken = default) =>
        RemoveContactAsync(owner, ignored, p => p.Ignores, cancellationToken);

    public bool IsIgnoring(int owner, int other) =>
        Find(owner) is { } player && player.Ignores.Any(i => i.AccountId == other);

    private async Task<ContactEntry?> AddContactAsync(int owner, int other, Func<PlayerRecord, List<ContactEntry>> list,
        CancellationToken cancellationToken)
    {
        if (Find(owner) is not { } player || owner == other)
        {
            return null;
        }

        var contacts = list(player);
        var existing = contacts.FirstOrDefault(c => c.AccountId == other);
        if (existing is not null)
        {
            return existing;
        }

        var entry = new ContactEntry(other, NameOf(other));
        contacts.Add(entry);
        await store.SaveAsync(player, cancellationToken);
        return entry;
    }

    private async Task<bool> RemoveContactAsync(int owner, int other, Func<PlayerRecord, List<ContactEntry>> list,
        CancellationToken cancellationToken)
    {
        if (Find(owner) is not { } player || list(player).RemoveAll(c => c.AccountId == other) == 0)
        {
            return false;
        }

        await store.SaveAsync(player, cancellationToken);
        return true;
    }

    public async Task<KarmaChange?> PenaliseAsync(int accountId, CancellationToken cancellationToken = default)
    {
        if (Find(accountId) is not { } player)
        {
            return null;
        }

        var old = player.Karma;
        player.Karma = karma.AfterWithdrawing(old);
        await store.SaveAsync(player, cancellationToken);
        return new KarmaChange(accountId, old, player.Karma);
    }

    public async Task<IReadOnlyList<ResultChange>> RecordRatedGameAsync(IReadOnlyList<GameResult> results,
        CancellationToken cancellationToken = default)
    {
        var known = results.Where(r => players.ContainsKey(r.AccountId)).ToList();
        var last = known.Count == 0 ? 0 : known.Max(r => r.Place) + 1;
        var places = known.Select(r => (players[r.AccountId].Rating, r.Withdrew ? last : r.Place)).ToList();
        var newRatings = rating.Update(places);

        var changes = new List<ResultChange>();
        for (var i = 0; i < known.Count; i++)
        {
            var player = players[known[i].AccountId];
            var oldRating = player.Rating;
            var oldGames = player.GamesPlayed;
            var oldKarma = player.Karma;
            player.Rating = Math.Round(newRatings[i], 2);
            player.GamesPlayed++;
            if (!known[i].Withdrew)
            {
                player.Karma = karma.AfterFinishing(player.Karma);
            }

            await store.SaveAsync(player, cancellationToken);
            changes.Add(new ResultChange(player.AccountId, oldRating, player.Rating, oldGames, player.GamesPlayed,
                oldKarma, player.Karma));
        }

        return changes;
    }
}
